using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Services;
using RAMQ.Samples.RoutingSlip.Booking.Message;
using System.Diagnostics;

namespace RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities
{
    /// <summary>
    /// Étape 1 : Réserver la voiture.
    ///
    /// <para>Résultats possibles :</para>
    /// <list type="bullet">
    ///   <item><see cref="ActivityResult.Next"/> — confirmation disponible, passer à ReserverHotel.</item>
    ///   <item><see cref="ActivityResult.Fault"/> — voiture indisponible (erreur permanente → DLQ).</item>
    ///   <item><see cref="ActivityResult.RetryExponential"/> — service de réservation indisponible (erreur transitoire).</item>
    /// </list>
    ///
    /// <para>Conventions de nommage pour les simulations :</para>
    /// <list type="table">
    ///   <item><term>INDISPO-xxx</term><description>Voiture indisponible → Fault direct.</description></item>
    ///   <item><term>TRANSIENT-xxx</term><description>Panne transitoire → RetryExponential les 2 premiers essais, puis succès.</description></item>
    /// </list>
    ///
    /// <para>
    /// Compensation : en cas de succès, une entrée est ajoutée dans Variables["CompensationLog"]
    /// afin que les étapes suivantes puissent annuler cette réservation si elles échouent.
    /// Cette étape étant la première du slip, elle n'a rien à compenser si elle échoue.
    /// </para>
    /// </summary>
    public class BookCarActivity : IRoutingSlipActivity<BookCarArgs>
    {
        private readonly ILogger<BookCarActivity> _logger;
        // Injectez ici votre ICarReservationApiClient

        public BookCarActivity(ILogger<BookCarActivity> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ActivityResult> ExecuteAsync(
            ActivityContext<BookCarArgs> ctx,
            CancellationToken ct)
        {
           
            _logger.LogInformation(
                "[{Step}] Tentative {Attempt} — ReservationId={Id}, Voiture={Car}, SlipId={SlipId}",
                ctx.StepName, ctx.Attempt, ctx.Arguments.ReservationId, ctx.Arguments.CarModel, ctx.SlipId);

            // Span métier — enfant du routing_slip.step émis par EMT.
            // Visible dans Jaeger : routing_slip.step > booking.car.reserve
            using var span = BookingTelemetry.Source.StartActivity("booking.car.reserve", ActivityKind.Client);
            span?.SetTag("booking.slip_id",        ctx.SlipId.ToString());
            span?.SetTag("booking.reservation_id", ctx.Arguments.ReservationId.ToString());
            span?.SetTag("booking.step",           ctx.StepName);
            span?.SetTag("booking.attempt",        ctx.Attempt);
            span?.SetTag("booking.car.model",      ctx.Arguments.CarModel);

            // ── Simulation : panne transitoire (TRANSIENT-) ─────────────────
            // ctx.Attempt est fourni par le framework (1 = premier essai, 2 = premier retry, …).
            // RetryExponential : Service Bus relivrera le message avec un délai croissant.
            // La panne disparaît au 3e essai — en production, ce serait une vraie erreur HTTP 503.
            if (ctx.Arguments.CarModel.StartsWith("TRANSIENT-", StringComparison.OrdinalIgnoreCase)
                && ctx.Attempt <= 2)
            {
                span?.SetStatus(ActivityStatusCode.Error, "Service voiture indisponible — RetryExponential planifié");
                span?.SetTag("booking.retry.type",   "RetryExponential");
                span?.SetTag("booking.retry.reason", "HTTP 503 Service Unavailable");
                span?.AddEvent(new ActivityEvent("retry.scheduled",
                    tags: new ActivityTagsCollection { ["attempt"] = ctx.Attempt, ["max_attempts"] = 3 }));
                _logger.LogWarning(
                    "[{Step}] Service voiture indisponible (transitoire) — Tentative {Attempt}/3, SlipId={SlipId}",
                    ctx.StepName, ctx.Attempt, ctx.SlipId);
                return ActivityResult.RetryExponential(
                    $"Service de réservation voiture indisponible — tentative {ctx.Attempt}",
                    new HttpRequestException("HTTP 503 Service Unavailable (SIMULATION)"));
            }

            // ── Simulation : panne permanente (CRASH-) → DLQ après épuisement ───────────────
            // Retourne TOUJOURS RetryExponential, quelle que soit la tentative.
            // Service Bus incrémente DeliveryCount à chaque relivraison.
            // Quand DeliveryCount > MaxDeliveryCount, le message est automatiquement envoyé en
            // Dead Letter Queue (DLQ) par le broker — sans intervention de l'application.
            // Ce scénario démontre que les retries ont un budget fini et que la DLQ doit être monitorée.
            if (ctx.Arguments.CarModel.StartsWith("CRASH-", StringComparison.OrdinalIgnoreCase))
            {
                span?.SetStatus(ActivityStatusCode.Error, "Panne permanente — DLQ après épuisement des retries");
                span?.SetTag("booking.retry.type",   "RetryExponential.Permanent");
                span?.SetTag("booking.retry.reason", "HTTP 504 Gateway Timeout");
                span?.AddEvent(new ActivityEvent("dlq.budget_consumption",
                    tags: new ActivityTagsCollection { ["attempt"] = ctx.Attempt }));
                _logger.LogWarning(
                    "[{Step}] Panne permanente (CRASH-) — Tentative {Attempt}, SlipId={SlipId}",
                    ctx.StepName, ctx.Attempt, ctx.SlipId);
                return ActivityResult.RetryExponential(
                    $"Service voiture inaccessible — panne permanente simulée (tentative {ctx.Attempt})",
                    new TimeoutException("HTTP 504 Gateway Timeout (SIMULATION CRASH-)"));
            }

            // ── Simulation : package VIP pré-confirmé → Complete() court-circuit ─────────────
            // Complete() interrompt le slip immédiatement : BookHotelActivity et BookFlightActivity
            // ne seront JAMAIS déclenchées.
            //
            // À NE PAS CONFONDRE avec Next() :
            //   Next()     = "j'ai terminé mon travail, passe à l'étape suivante (ou termine si je suis la dernière)"
            //   Complete() = "stop — on n'exécute plus aucune étape, même s'il en reste"
            //
            // Cas d'usage réel : idempotence ("déjà traité lors d'une run précédente"),
            // condition de sortie anticipée (accès premium pré-vérifié), A/B routing conditionnel.
            if (ctx.Arguments.CarModel.StartsWith("VIP-", StringComparison.OrdinalIgnoreCase))
            {
                span?.SetTag("booking.car.vip_package", true);
                span?.SetTag("booking.vip.model",      ctx.Arguments.CarModel["VIP-".Length..]);
                span?.AddEvent(new ActivityEvent("booking.vip.court_circuit_complete"));
                span?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogInformation(
                    "[{Step}] Package VIP pré-confirmé détecté — slip terminé en avance (Complete). "
                    + "Hôtel et vol ne seront pas appelés. SlipId={SlipId}",
                    ctx.StepName, ctx.SlipId);
                return ActivityResult.Complete();
            }

            // SIMULATION : remplacer par votre ICarReservationApiClient
            await Task.Delay(50, ct);
            var confirmation = await SimulerReservationVoiture(ctx.Arguments, ct);

            if (confirmation is null)
            {
                // Première étape du slip : rien à compenser.
                span?.SetStatus(ActivityStatusCode.Error, "Voiture indisponible — Fault → DLQ");
                span?.SetTag("booking.car.available", false);
                span?.SetTag("error.type",           "Fault");
                _logger.LogError(
                    "[{Step}] Voiture '{Model}' indisponible → Fault. SlipId={SlipId}",
                    ctx.StepName, ctx.Arguments.CarModel, ctx.SlipId);
                return ActivityResult.Fault(
                    new InvalidOperationException(
                        $"Voiture '{ctx.Arguments.CarModel}' indisponible pour ReservationId={ctx.Arguments.ReservationId}."));
            }

            if (ctx.Attempt > 1)
                _logger.LogInformation(
                    "[{Step}] Succès après {Attempt} tentatives — SlipId={SlipId}",
                    ctx.StepName, ctx.Attempt, ctx.SlipId);

            span?.SetTag("booking.car.confirmation_id", confirmation);
            span?.SetTag("booking.car.available",       true);
            span?.SetStatus(ActivityStatusCode.Ok);

            // Succès : partager la confirmation ET enregistrer l'entrée de compensation.
            var entreeCompensation = new CompensationLogEntry(
                StepName:       ctx.StepName,
                ConfirmationId: confirmation,
                ServiceType:    "Voiture");

            return ActivityResult.Next(vars =>
            {
                vars["ConfirmationVoiture"]    = confirmation;
                vars["DateReservationVoiture"] = DateTime.UtcNow;
                vars["CompensationLog"]        = new List<CompensationLogEntry> { entreeCompensation };
            });
        }

        private static Task<string?> SimulerReservationVoiture(BookCarArgs args, CancellationToken ct)
        {
            // INDISPO- : erreur permanente
            // TRANSIENT- (après 2 retries résolus) : succès — la simulation est pilotée par ctx.Attempt
            var estIndispo = args.CarModel.StartsWith("INDISPO-", StringComparison.OrdinalIgnoreCase);
            var code = estIndispo
                ? null
                : $"CAR-{args.ReservationId:N}";
            return Task.FromResult(code);
        }
    }
}
