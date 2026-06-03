using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Services;
using RAMQ.Samples.RoutingSlip.Booking.Message;
using System.Diagnostics;

namespace RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities
{
    /// <summary>
    /// Étape 3 (finale) : Réserver le vol.
    ///
    /// <para>
    /// Dernière étape du slip — <see cref="ActivityResult.Next"/> suffit, le framework
    /// complète le slip automatiquement (pas besoin d'appeler Complete() explicitement).
    /// Lit les confirmations des étapes précédentes depuis Variables.
    /// </para>
    ///
    /// <para>Résultats possibles :</para>
    /// <list type="bullet">
    ///   <item><see cref="ActivityResult.Next"/> — vol réservé, workflow terminé avec succès.</item>
    ///   <item><see cref="ActivityResult.Fault"/> — vol annulé (erreur permanente → DLQ + compensation).</item>
    ///   <item><see cref="ActivityResult.RetryImmediate"/> — API saturée (throttling transitoire court).</item>
    ///   <item><see cref="ActivityResult.RetryExponential"/> — compagnie aérienne indisponible (panne longue).</item>
    /// </list>
    ///
    /// <para>Conventions de nommage pour les simulations :</para>
    /// <list type="table">
    ///   <item><term>ANNULE-xxx</term><description>Vol annulé → Fault + compensation LIFO.</description></item>
    ///   <item><term>THROTTLE-xxx</term><description>API saturée → RetryImmediate au 1er essai, succès au 2e.</description></item>
    /// </list>
    ///
    /// <para>
    /// Compensation : en cas d'échec, le journal est lu depuis Variables et toutes les opérations
    /// précédentes (hôtel puis voiture) sont annulées en ordre inverse (LIFO) avant de retourner Fault().
    /// Si une compensation individuelle échoue, l'erreur est loggée et les autres sont quand même tentées
    /// (best-effort). Un log Critical est émis pour chaque compensation en échec.
    /// </para>
    /// </summary>
    public class BookFlightActivity : IRoutingSlipActivity<BookFlightArgs>
    {
        private readonly ILogger<BookFlightActivity> _logger;
        private readonly IBookingCompensationService _compensation;
        // Injectez ici votre IFlightReservationApiClient

        public BookFlightActivity(
            ILogger<BookFlightActivity> logger,
            IBookingCompensationService compensation)
        {
            _logger       = logger       ?? throw new ArgumentNullException(nameof(logger));
            _compensation = compensation ?? throw new ArgumentNullException(nameof(compensation));
        }

        public async Task<ActivityResult> ExecuteAsync(
            ActivityContext<BookFlightArgs> ctx,
            CancellationToken ct)
        {
            var confirmationVoiture = ctx.GetVariable<string>("ConfirmationVoiture");
            var confirmationHotel   = ctx.GetVariable<string>("ConfirmationHotel");

            _logger.LogInformation(
                "[{Step}] Tentative {Attempt} — ReservationId={Id}, Vol={Flight}, ConfVoiture={CV}, ConfHotel={CH}, SlipId={SlipId}",
                ctx.StepName, ctx.Attempt, ctx.Arguments.ReservationId, ctx.Arguments.FlightName,
                confirmationVoiture, confirmationHotel, ctx.SlipId);

            // Span métier — enfant du routing_slip.step émis par EMT.
            // Visible dans Jaeger : routing_slip.step > booking.flight.reserve
            using var span = BookingTelemetry.Source.StartActivity("booking.flight.reserve", ActivityKind.Client);
            span?.SetTag("booking.slip_id",        ctx.SlipId.ToString());
            span?.SetTag("booking.reservation_id", ctx.Arguments.ReservationId.ToString());
            span?.SetTag("booking.step",           ctx.StepName);
            span?.SetTag("booking.attempt",        ctx.Attempt);
            span?.SetTag("booking.flight.name",    ctx.Arguments.FlightName);

            // ── Simulation : throttling API (THROTTLE-) → RetryImmediate ───────────────
            // RetryImmediate vs RetryExponential — différences clés :
            //   • RetryImmediate : Service Bus redelivre le message SANS délai (de l'ordre de quelques ms)
            //     → À utiliser pour les contentions très brèves (rate-limit, throttling court)
            //     → Exemple : API surchargée, retrouve sa capacité avant le 2e essai
            //   • RetryExponential : Service Bus redelivre avec délai exponentiel (délai = 2^tentative)
            //     → À utiliser pour les pannes longues (service indisponible, DB hors ligne)
            //     → Exemple : Compagnie aérienne down pendant plusieurs minutes
            // Dans ce scénario, la congestion disparaît au 2e essai immédiat.
            if (ctx.Arguments.FlightName.StartsWith("THROTTLE-", StringComparison.OrdinalIgnoreCase)
                && ctx.Attempt == 1)
            {
                _logger.LogWarning(
                    "[{Step}] API vol saturée (throttling) — RetryImmediate, SlipId={SlipId}",
                    ctx.StepName, ctx.SlipId);
                return ActivityResult.RetryImmediate("API de réservation vol saturée — HTTP 429 (SIMULATION)");
            }

            // SIMULATION : remplacer par votre IFlightReservationApiClient
            await Task.Delay(100, ct);
            var confirmation = await SimulerReservationVol(ctx.Arguments, ct);

            if (confirmation is null)
            {
                // Compensation : annuler hôtel puis voiture (ordre LIFO garanti par CompensateAsync)
                span?.SetStatus(ActivityStatusCode.Error, "Vol annulé — Fault → compensation + DLQ");
                span?.SetTag("booking.flight.available", false);
                span?.SetTag("error.type",               "Fault");
                _logger.LogWarning(
                    "[{Step}] Vol '{Flight}' annulé → Fault + compensation. SlipId={SlipId}",
                    ctx.StepName, ctx.Arguments.FlightName, ctx.SlipId);

                var log = ctx.GetVariable<List<CompensationLogEntry>>("CompensationLog") ?? [];

                // Span compensation — enfant du span booking.flight.reserve actif.
                using (var compensateSpan = BookingTelemetry.Source.StartActivity("booking.compensate", ActivityKind.Internal))
                {
                    compensateSpan?.SetTag("booking.slip_id",                    ctx.SlipId.ToString());
                    compensateSpan?.SetTag("booking.step",                       ctx.StepName);
                    compensateSpan?.SetTag("booking.compensation.entries_count", log.Count);

                    var echecs = await _compensation.CompensateAsync(log, ctx.SlipId, ct);
                    if (echecs.Count > 0)
                    {
                        compensateSpan?.SetStatus(ActivityStatusCode.Error,
                            $"{echecs.Count} annulation(s) en échec — intervention manuelle requise");
                        compensateSpan?.SetTag("booking.compensation.failures", echecs.Count);
                        _logger.LogCritical(
                            "[{Step}] Compensation PARTIELLE — {Count} annulation(s) en échec. "
                            + "Étapes non compensées : {Etapes}. SlipId={SlipId}",
                            ctx.StepName, echecs.Count,
                            string.Join(", ", echecs.Select(e => e.StepName)),
                            ctx.SlipId);
                    }
                    else
                    {
                        compensateSpan?.SetStatus(ActivityStatusCode.Ok);
                        compensateSpan?.SetTag("booking.compensation.failures", 0);
                    }
                }

                return ActivityResult.Fault(
                    new InvalidOperationException(
                        $"Vol '{ctx.Arguments.FlightName}' annulé pour ReservationId={ctx.Arguments.ReservationId}."));
            }

            if (ctx.Attempt > 1)
                _logger.LogInformation(
                    "[{Step}] Succès après {Attempt} tentatives — SlipId={SlipId}",
                    ctx.StepName, ctx.Attempt, ctx.SlipId);

            span?.SetTag("booking.flight.confirmation_id", confirmation);
            span?.SetTag("booking.flight.available",       true);
            span?.SetStatus(ActivityStatusCode.Ok);

            // Dernière étape — pas besoin d'ajouter au CompensationLog (workflow terminé)
            // Next() déclenche la complétion automatique par le framework
            return ActivityResult.Next(vars =>
            {
                vars["ConfirmationVol"]     = confirmation;
                vars["DateReservationVol"]  = DateTime.UtcNow;
                vars["BookingStatut"]       = "CompletAvecSucces";
                vars["ConfirmationVoiture"] = confirmationVoiture ?? string.Empty;
                vars["ConfirmationHotel"]   = confirmationHotel   ?? string.Empty;
            });
        }

        private static Task<string?> SimulerReservationVol(BookFlightArgs args, CancellationToken ct)
        {
            // ANNULE- : erreur permanente
            // THROTTLE- (après retry résolu) : succès — piloté par ctx.Attempt
            var estAnnule = args.FlightName.StartsWith("ANNULE-", StringComparison.OrdinalIgnoreCase);
            var code = estAnnule
                ? null
                : $"FLT-{args.ReservationId:N}";
            return Task.FromResult(code);
        }
    }
}
