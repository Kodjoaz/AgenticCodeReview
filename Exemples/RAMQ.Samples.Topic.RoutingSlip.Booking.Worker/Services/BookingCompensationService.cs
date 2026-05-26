using Microsoft.Extensions.Logging;
using RAMQ.Samples.RoutingSlip.Booking.Message;

namespace RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Services
{
    /// <summary>
    /// Implémentation du service de compensation pour le workflow de réservation (variante Topic).
    ///
    /// <para>
    /// Applique les compensations dans l'ordre <b>inverse</b> du journal (LIFO),
    /// garantissant que la dernière opération réussie est défaite en premier.
    /// </para>
    ///
    /// <para>
    /// En production, remplacer chaque méthode <c>Annuler*Async</c> par l'appel
    /// réel à l'API externe via le client HTTP correspondant.
    /// </para>
    /// </summary>
    public class BookingCompensationService : IBookingCompensationService
    {
        private readonly ILogger<BookingCompensationService> _logger;

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
                    _logger.LogCritical(ex,
                        "[Compensation] ÉCHEC d'annulation — Étape={Step}, ConfirmationId={Id}, SlipId={SlipId}. " +
                        "Intervention manuelle requise.",
                        entry.StepName, entry.ConfirmationId, slipId);
                    echecs.Add(entry);
                }
            }

            return echecs;
        }

        private async Task AnnulerVoitureAsync(string confirmationId, string slipId, CancellationToken ct)
        {
            await Task.Delay(30, ct);
            _logger.LogInformation(
                "[Compensation] ✓ Voiture annulée — Confirmation={Id}, SlipId={SlipId}",
                confirmationId, slipId);
        }

        private async Task AnnulerHotelAsync(string confirmationId, string slipId, CancellationToken ct)
        {
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
            await Task.Delay(30, ct);
            _logger.LogInformation(
                "[Compensation] ✓ Vol annulé — Confirmation={Id}, SlipId={SlipId}",
                confirmationId, slipId);
        }
    }
}
