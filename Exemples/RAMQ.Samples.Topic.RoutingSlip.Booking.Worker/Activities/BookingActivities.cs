using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Services;
using RAMQ.Samples.RoutingSlip.Booking.Message;
using System.Diagnostics;

namespace RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Activities
{
    /// <summary>
    /// Étape 1 : Réserver la voiture (variante Topic — logique identique à la version Queue).
    ///
    /// <para>
    /// Le transport (Queue vs Topic) est transparent pour l'activité.
    /// Seul le Program.cs et les triggers Functions diffèrent entre Queue et Topic.
    /// </para>
    ///
    /// <para>Conventions de simulation :</para>
    /// <list type="table">
    ///   <item><term>INDISPO-xxx</term><description>Fault direct.</description></item>
    ///   <item><term>TRANSIENT-xxx</term><description>RetryExponential les 2 premiers essais, succès au 3e.</description></item>
    /// </list>
    /// </summary>
    public class BookCarActivity : IRoutingSlipActivity<BookCarArgs>
    {
        private readonly ILogger<BookCarActivity> _logger;

        public BookCarActivity(ILogger<BookCarActivity> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ActivityResult> ExecuteAsync(
            ActivityContext<BookCarArgs> ctx,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "[{Step}] Tentative {Attempt} (Topic) — ReservationId={Id}, Modèle={Model}, SlipId={SlipId}",
                ctx.StepName, ctx.Attempt, ctx.Arguments.ReservationId, ctx.Arguments.CarModel, ctx.SlipId);

            using var span = BookingTelemetry.Source.StartActivity("booking.car.reserve", ActivityKind.Client);
            span?.SetTag("booking.slip_id",        ctx.SlipId.ToString());
            span?.SetTag("booking.reservation_id", ctx.Arguments.ReservationId.ToString());
            span?.SetTag("booking.step",           ctx.StepName);
            span?.SetTag("booking.attempt",        ctx.Attempt);
            span?.SetTag("booking.car.model",      ctx.Arguments.CarModel);

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
            if (ctx.Arguments.CarModel.StartsWith("CRASH-", StringComparison.OrdinalIgnoreCase))
            {
                span?.SetStatus(ActivityStatusCode.Error, "Panne permanente — DLQ après épuisement des retries");
                span?.SetTag("booking.retry.type",   "RetryExponential.Permanent");
                span?.SetTag("booking.retry.reason", "HTTP 504 Gateway Timeout");
                span?.AddEvent(new ActivityEvent("dlq.budget_consumption",
                    tags: new ActivityTagsCollection { ["attempt"] = ctx.Attempt }));
                _logger.LogError(
                    "[{Step}] Service voiture en panne permanente (CRASH-) — tentative {Attempt}, SlipId={SlipId}",
                    ctx.StepName, ctx.Attempt, ctx.SlipId);
                return ActivityResult.RetryExponential(
                    $"Service voiture inaccessible — panne permanente simulée (tentative {ctx.Attempt})",
                    new TimeoutException("HTTP 504 Gateway Timeout (SIMULATION CRASH-)"));
            }

            // ── Simulation : package VIP pré-confirmé → Complete() court-circuit ─────────────
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

            await Task.Delay(50, ct);

            var estIndispo = ctx.Arguments.CarModel.StartsWith("INDISPO-", StringComparison.OrdinalIgnoreCase);
            var confirmation = estIndispo ? null : $"CAR-{ctx.Arguments.ReservationId:N}";

            if (confirmation is null)
            {
                span?.SetStatus(ActivityStatusCode.Error, "Voiture indisponible — Fault → DLQ");
                span?.SetTag("booking.car.available", false);
                span?.SetTag("error.type",           "Fault");
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
    }

    /// <summary>
    /// Étape 2 : Réserver l'hôtel (variante Topic).
    ///
    /// <para>Conventions de simulation :</para>
    /// <list type="table">
    ///   <item><term>COMPLET-xxx</term><description>Fault + compensation voiture.</description></item>
    ///   <item><term>TRANSIENT-xxx</term><description>RetryExponential les 2 premiers essais, succès au 3e.</description></item>
    ///   <item><term>COMPFAIL-xxx</term><description>Réservation OK, mais l'annulation échouera si compensation déclenchée.</description></item>
    /// </list>
    /// </summary>
    public class BookHotelActivity : IRoutingSlipActivity<BookHotelArgs>
    {
        private readonly ILogger<BookHotelActivity> _logger;
        private readonly IBookingCompensationService _compensation;

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
            var confirmationVoiture = ctx.GetVariable<string>("ConfirmationVoiture");

            _logger.LogInformation(
                "[{Step}] Tentative {Attempt} (Topic) — ReservationId={Id}, Hôtel={Hotel}, ConfVoiture={CV}, SlipId={SlipId}",
                ctx.StepName, ctx.Attempt, ctx.Arguments.ReservationId,
                ctx.Arguments.HotelName, confirmationVoiture, ctx.SlipId);

            using var span = BookingTelemetry.Source.StartActivity("booking.hotel.reserve", ActivityKind.Client);
            span?.SetTag("booking.slip_id",        ctx.SlipId.ToString());
            span?.SetTag("booking.reservation_id", ctx.Arguments.ReservationId.ToString());
            span?.SetTag("booking.step",           ctx.StepName);
            span?.SetTag("booking.attempt",        ctx.Attempt);
            span?.SetTag("booking.hotel.name",     ctx.Arguments.HotelName);

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

            await Task.Delay(75, ct);

            string? confirmation;
            if (ctx.Arguments.HotelName.StartsWith("COMPLET-", StringComparison.OrdinalIgnoreCase))
                confirmation = null;
            else if (ctx.Arguments.HotelName.StartsWith("COMPFAIL-", StringComparison.OrdinalIgnoreCase))
                confirmation = $"COMPFAIL-HTL-{ctx.Arguments.ReservationId:N}";
            else
                confirmation = $"HTL-{ctx.Arguments.ReservationId:N}";

            if (confirmation is null)
            {
                span?.SetStatus(ActivityStatusCode.Error, "Hôtel complet — Fault → compensation + DLQ");
                span?.SetTag("booking.hotel.available", false);
                span?.SetTag("error.type",              "Fault");

                var logExistant = ctx.GetVariable<List<CompensationLogEntry>>("CompensationLog") ?? [];

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
    }

    /// <summary>
    /// Étape 3 (finale) : Réserver le vol (variante Topic).
    ///
    /// <para>Conventions de simulation :</para>
    /// <list type="table">
    ///   <item><term>ANNULE-xxx</term><description>Fault + compensation LIFO.</description></item>
    ///   <item><term>THROTTLE-xxx</term><description>RetryImmediate au 1er essai, succès au 2e.</description></item>
    /// </list>
    /// </summary>
    public class BookFlightActivity : IRoutingSlipActivity<BookFlightArgs>
    {
        private readonly ILogger<BookFlightActivity> _logger;
        private readonly IBookingCompensationService _compensation;

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
                "[{Step}] Tentative {Attempt} (Topic) — ReservationId={Id}, Vol={Flight}, SlipId={SlipId}",
                ctx.StepName, ctx.Attempt, ctx.Arguments.ReservationId, ctx.Arguments.FlightName, ctx.SlipId);

            using var span = BookingTelemetry.Source.StartActivity("booking.flight.reserve", ActivityKind.Client);
            span?.SetTag("booking.slip_id",        ctx.SlipId.ToString());
            span?.SetTag("booking.reservation_id", ctx.Arguments.ReservationId.ToString());
            span?.SetTag("booking.step",           ctx.StepName);
            span?.SetTag("booking.attempt",        ctx.Attempt);
            span?.SetTag("booking.flight.name",    ctx.Arguments.FlightName);

            if (ctx.Arguments.FlightName.StartsWith("THROTTLE-", StringComparison.OrdinalIgnoreCase)
                && ctx.Attempt == 1)
            {
                span?.SetStatus(ActivityStatusCode.Error, "API vol saturée — RetryImmediate planifié");
                span?.SetTag("booking.retry.type",   "RetryImmediate");
                span?.SetTag("booking.retry.reason", "HTTP 429 Too Many Requests");
                span?.AddEvent(new ActivityEvent("retry.scheduled",
                    tags: new ActivityTagsCollection { ["attempt"] = ctx.Attempt }));
                _logger.LogWarning(
                    "[{Step}] API vol saturée (throttling) — RetryImmediate, SlipId={SlipId}",
                    ctx.StepName, ctx.SlipId);
                return ActivityResult.RetryImmediate("API de réservation vol saturée — HTTP 429 (SIMULATION)");
            }

            await Task.Delay(100, ct);

            var estAnnule = ctx.Arguments.FlightName.StartsWith("ANNULE-", StringComparison.OrdinalIgnoreCase);
            var confirmation = estAnnule ? null : $"FLT-{ctx.Arguments.ReservationId:N}";

            if (confirmation is null)
            {
                span?.SetStatus(ActivityStatusCode.Error, "Vol annulé — Fault → compensation + DLQ");
                span?.SetTag("booking.flight.available", false);
                span?.SetTag("error.type",               "Fault");

                var log = ctx.GetVariable<List<CompensationLogEntry>>("CompensationLog") ?? [];

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

            return ActivityResult.Next(vars =>
            {
                vars["ConfirmationVol"]     = confirmation;
                vars["DateReservationVol"]  = DateTime.UtcNow;
                vars["BookingStatut"]       = "CompletAvecSucces";
                vars["ConfirmationVoiture"] = confirmationVoiture ?? string.Empty;
                vars["ConfirmationHotel"]   = confirmationHotel   ?? string.Empty;
            });
        }
    }
}
