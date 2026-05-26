using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Services;
using RAMQ.Samples.RoutingSlip.Booking.Message;
using System.Diagnostics;

namespace RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities
{
    /// <summary>
    /// Étape 2 : Réserver l'hôtel.
    ///
    /// <para>Lit la confirmation de l'étape précédente depuis Variables.</para>
    ///
    /// <para>Résultats possibles :</para>
    /// <list type="bullet">
    ///   <item><see cref="ActivityResult.Next"/> — hôtel réservé, passer à ReserverVol.</item>
    ///   <item><see cref="ActivityResult.Fault"/> — hôtel complet (erreur permanente → DLQ + compensation).</item>
    ///   <item><see cref="ActivityResult.RetryExponential"/> — service indisponible (erreur transitoire).</item>
    /// </list>
    ///
    /// <para>Conventions de nommage pour les simulations :</para>
    /// <list type="table">
    ///   <item><term>COMPLET-xxx</term><description>Hôtel complet → Fault + compensation voiture.</description></item>
    ///   <item><term>TRANSIENT-xxx</term><description>Panne transitoire → RetryExponential les 2 premiers essais, puis succès.</description></item>
    ///   <item><term>COMPFAIL-xxx</term><description>Réservation OK, mais l'annulation échouera si compensation déclenchée (scénario échec-compensation).</description></item>
    /// </list>
    ///
    /// <para>
    /// Compensation : en cas d'échec, le journal de compensation est lu depuis Variables
    /// et toutes les opérations précédentes (voiture) sont annulées avant de retourner Fault().
    /// En cas de succès, l'entrée hôtel est ajoutée au journal pour l'étape suivante.
    /// </para>
    /// </summary>
    public class BookHotelActivity : IRoutingSlipActivity<BookHotelArgs>
    {
        private readonly ILogger<BookHotelActivity> _logger;
        private readonly IBookingCompensationService _compensation;
        // Injectez ici votre IHotelReservationApiClient

        public BookHotelActivity(
            ILogger<BookHotelActivity> logger,
            IBookingCompensationService compensation)
        {
            _logger       = logger       ?? throw new ArgumentNullException(nameof(logger));
            _compensation = compensation ?? throw new ArgumentNullException(nameof(compensation));
        }

        public async Task<ActivityResult> ExecuteAsync(
            ActivityContext<BookHotelArgs> ctx,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "[TRACE] Entrée dans ExecuteAsync — Step={Step}, Attempt={Attempt}, HotelName={HotelName}, SlipId={SlipId}",
                ctx.StepName, ctx.Attempt, ctx.Arguments.HotelName, ctx.SlipId);

            var confirmationVoiture = ctx.GetVariable<string>("ConfirmationVoiture");

            _logger.LogInformation(
                "[{Step}] Tentative {Attempt} — ReservationId={Id}, Hôtel={Hotel}, ConfVoiture={CV}, SlipId={SlipId}",
                ctx.StepName, ctx.Attempt, ctx.Arguments.ReservationId,
                ctx.Arguments.HotelName, confirmationVoiture, ctx.SlipId);

            // Span métier — enfant du routing_slip.step émis par EMT.
            using var span = BookingTelemetry.Source.StartActivity("booking.hotel.reserve", ActivityKind.Client);

            // Log pour vérifier la condition TRANSIENT-
            if (!ctx.Arguments.HotelName.StartsWith("TRANSIENT-", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[TRACE] HotelName ne commence PAS par TRANSIENT- : {HotelName}", ctx.Arguments.HotelName);
            }
            else if (ctx.Attempt > 2)
            {
                _logger.LogInformation("[TRACE] Tentative > 2 pour TRANSIENT- : Attempt={Attempt}, HotelName={HotelName}", ctx.Attempt, ctx.Arguments.HotelName);
            }
            span?.SetTag("booking.slip_id",        ctx.SlipId.ToString());
            span?.SetTag("booking.reservation_id", ctx.Arguments.ReservationId.ToString());
            span?.SetTag("booking.step",           ctx.StepName);
            span?.SetTag("booking.attempt",        ctx.Attempt);
            span?.SetTag("booking.hotel.name",     ctx.Arguments.HotelName);

            // ── Simulation : panne transitoire (TRANSIENT-) ─────────────────
            if (ctx.Arguments.HotelName.StartsWith("TRANSIENT-", StringComparison.OrdinalIgnoreCase)
                && ctx.Attempt <= 2)
            {
                span?.SetStatus(ActivityStatusCode.Error, "Service hôtel indisponible — RetryExponential planifié");
                span?.SetTag("booking.retry.type",   "RetryExponential");
                span?.SetTag("booking.retry.reason", "HTTP 503 Service Unavailable");
                span?.AddEvent(new ActivityEvent("retry.scheduled",
                    tags: new ActivityTagsCollection { ["attempt"] = ctx.Attempt, ["max_attempts"] = 3 }));
                _logger.LogWarning(
                    "[{Step}] Service hôtel indisponible (transitoire) — Tentative {Attempt}/3, SlipId={SlipId}",
                    ctx.StepName, ctx.Attempt, ctx.SlipId);
                return ActivityResult.RetryExponential(
                    $"Service de réservation hôtel indisponible — tentative {ctx.Attempt}",
                    new HttpRequestException("HTTP 503 Service Unavailable (SIMULATION)"));
            }

            // SIMULATION : remplacer par votre IHotelReservationApiClient
            await Task.Delay(75, ct);
            var confirmation = await SimulerReservationHotel(ctx.Arguments, ct);

            if (confirmation is null)
            {
                // Compensation : annuler la voiture déjà réservée avant de déclarer l'échec
                span?.SetStatus(ActivityStatusCode.Error, "Hôtel complet — Fault → compensation + DLQ");
                span?.SetTag("booking.hotel.available", false);
                span?.SetTag("error.type",              "Fault");

                var logExistant = ctx.GetVariable<List<CompensationLogEntry>>("CompensationLog") ?? [];

                // Span compensation — enfant du span booking.hotel.reserve actif.
                // Permet d'observer la durée et le résultat du rollback dans Jaeger.
                using (var compensateSpan = BookingTelemetry.Source.StartActivity("booking.compensate", ActivityKind.Internal))
                {
                    compensateSpan?.SetTag("booking.slip_id",                    ctx.SlipId.ToString());
                    compensateSpan?.SetTag("booking.step",                       ctx.StepName);
                    compensateSpan?.SetTag("booking.compensation.entries_count", logExistant.Count);

                    var echecs = await _compensation.CompensateAsync(logExistant, ctx.SlipId, ct);
                    if (echecs.Count > 0)
                    {
                        compensateSpan?.SetStatus(ActivityStatusCode.Error,
                            $"{echecs.Count} annulation(s) en échec — intervention manuelle requise");
                        compensateSpan?.SetTag("booking.compensation.failures", echecs.Count);
                        _logger.LogCritical(
                            "[{Step}] Compensation PARTIELLE — {Count} annulation(s) en échec. SlipId={SlipId}",
                            ctx.StepName, echecs.Count, ctx.SlipId);
                    }
                    else
                    {
                        compensateSpan?.SetStatus(ActivityStatusCode.Ok);
                        compensateSpan?.SetTag("booking.compensation.failures", 0);
                    }
                }

                return ActivityResult.Fault(
                    new InvalidOperationException(
                        $"Hôtel '{ctx.Arguments.HotelName}' complet pour ReservationId={ctx.Arguments.ReservationId}."));
            }

            if (ctx.Attempt > 1)
                _logger.LogInformation(
                    "[{Step}] Succès après {Attempt} tentatives — SlipId={SlipId}",
                    ctx.StepName, ctx.Attempt, ctx.SlipId);

            span?.SetTag("booking.hotel.confirmation_id", confirmation);
            span?.SetTag("booking.hotel.available",       true);
            span?.SetStatus(ActivityStatusCode.Ok);

            // Succès : ajouter l'entrée hôtel au journal de compensation pour ReserverVol.
            // Note : si HotelName commence par COMPFAIL-, le ConfirmationId héritera du préfixe,
            // ce qui fera échouer l'annulation si compensation déclenchée plus tard.
            var logMisAJour = ctx.GetVariable<List<CompensationLogEntry>>("CompensationLog") ?? [];
            logMisAJour.Add(new CompensationLogEntry(
                StepName:       ctx.StepName,
                ConfirmationId: confirmation,
                ServiceType:    "Hotel"));

            return ActivityResult.Next(vars =>
            {
                vars["ConfirmationHotel"]    = confirmation;
                vars["DateReservationHotel"] = DateTime.UtcNow;
                vars["CompensationLog"]      = logMisAJour;
            });
        }

        private static Task<string?> SimulerReservationHotel(BookHotelArgs args, CancellationToken ct)
        {
            // COMPLET- : erreur permanente
            if (args.HotelName.StartsWith("COMPLET-", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<string?>(null);

            // COMPFAIL- : réservation OK, mais le ConfirmationId est préfixé pour déclencher
            //             un échec lors de l'annulation par BookingCompensationService.
            if (args.HotelName.StartsWith("COMPFAIL-", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<string?>($"COMPFAIL-HTL-{args.ReservationId:N}");

            return Task.FromResult<string?>($"HTL-{args.ReservationId:N}");
        }
    }
}
