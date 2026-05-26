using Microsoft.Extensions.Logging;
using RAMQ.Samples.RoutingSlip.Booking.Message;

namespace RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Services
{
    /// <summary>
    /// Implémentation du service de compensation pour le workflow de réservation.
    ///
    /// <para>
    /// Ce service applique les compensations dans l'ordre <b>inverse</b> du journal,
    /// garantissant que la dernière opération réussie est défaite en premier.
    /// </para>
    ///
    /// <para>
    /// En production, remplacer chaque méthode <c>Annuler*Async</c> par l'appel
    /// réel à l'API externe via le client HTTP correspondant
    /// (ex: <c>ICarReservationApiClient</c>, <c>IHotelApiClient</c>…).
    /// La signature des méthodes reste inchangée — seule l'implémentation change.
    /// </para>
    /// </summary>
    public class BookingCompensationService : IBookingCompensationService
    {
        private readonly ILogger<BookingCompensationService> _logger;
        // En production, injectez ICarReservationApiClient, IHotelApiClient, IFlightApiClient

        public BookingCompensationService(ILogger<BookingCompensationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CompensationLogEntry>> CompensateAsync(
            IEnumerable<CompensationLogEntry> log,
            string slipId,
            CancellationToken ct)
        {
            var echecs = new List<CompensationLogEntry>();

            // LIFO : annuler dans l'ordre inverse pour respecter les dépendances
            foreach (var entry in log.Reverse())
            {
                _logger.LogWarning(
                    "[Compensation] Annulation de l'étape {Step} — ConfirmationId={Id}, SlipId={SlipId}",
                    entry.StepName, entry.ConfirmationId, slipId);
                try
                {
                    switch (entry.ServiceType)
                    {
                        case "Voiture":
                            await AnnulerVoitureAsync(entry.ConfirmationId, slipId, ct);
                            break;

                        case "Hotel":
                            await AnnulerHotelAsync(entry.ConfirmationId, slipId, ct);
                            break;

                        case "Vol":
                            await AnnulerVolAsync(entry.ConfirmationId, slipId, ct);
                            break;

                        default:
                            _logger.LogError(
                                "[Compensation] ServiceType inconnu '{Type}' — étape {Step} non compensée. SlipId={SlipId}",
                                entry.ServiceType, entry.StepName, slipId);
                            echecs.Add(entry);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Best-effort : l'échec d'une compensation ne doit pas bloquer les autres.
                    // L'entrée est collectée pour que l'appelant puisse logger / alerter.
                    _logger.LogCritical(ex,
                        "[Compensation] ÉCHEC d'annulation — Étape={Step}, ConfirmationId={Id}, SlipId={SlipId}. " +
                        "Intervention manuelle requise.",
                        entry.StepName, entry.ConfirmationId, slipId);
                    echecs.Add(entry);
                }
            }

            return echecs;
        }

        // ── Méthodes de compensation individuelles ────────────────────────────────
        // En production : remplacer Task.Delay par l'appel HTTP réel.

        private async Task AnnulerVoitureAsync(string confirmationId, string slipId, CancellationToken ct)
        {
            // SIMULATION : remplacer par ICarReservationApiClient.CancelAsync(confirmationId)
            await Task.Delay(30, ct);
            _logger.LogInformation(
                "[Compensation] ✓ Voiture annulée — Confirmation={Id}, SlipId={SlipId}",
                confirmationId, slipId);
        }

        private async Task AnnulerHotelAsync(string confirmationId, string slipId, CancellationToken ct)
        {
            // SIMULATION COMPFAIL : si le ConfirmationId commence par "COMPFAIL-",
            // l'API d'annulation échoue — démontre la gestion d'un échec de compensation.
            // En production : remplacer par IHotelApiClient.CancelAsync(confirmationId)
            if (confirmationId.StartsWith("COMPFAIL-", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"API d'annulation hôtel indisponible (SIMULATION) — Confirmation={confirmationId}, SlipId={slipId}");

            await Task.Delay(30, ct);
            _logger.LogInformation(
                "[Compensation] ✓ Hôtel annulé — Confirmation={Id}, SlipId={SlipId}",
                confirmationId, slipId);
        }

        private async Task AnnulerVolAsync(string confirmationId, string slipId, CancellationToken ct)
        {
            // SIMULATION : remplacer par IFlightApiClient.CancelAsync(confirmationId)
            await Task.Delay(30, ct);
            _logger.LogInformation(
                "[Compensation] ✓ Vol annulé — Confirmation={Id}, SlipId={SlipId}",
                confirmationId, slipId);
        }
    }
}
