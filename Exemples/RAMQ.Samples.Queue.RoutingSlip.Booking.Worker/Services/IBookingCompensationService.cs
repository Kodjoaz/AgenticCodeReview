using RAMQ.Samples.RoutingSlip.Booking.Message;

namespace RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Services
{
    /// <summary>
    /// Contrat du service de compensation pour le workflow de réservation.
    ///
    /// <para>
    /// Ce service est injecté dans les activités qui peuvent échouer après
    /// que des étapes précédentes ont déjà effectué des opérations.
    /// Il lit le journal de compensation (<see cref="CompensationLogEntry"/>)
    /// accumulé dans les Variables du slip et annule chaque opération dans
    /// l'ordre inverse (LIFO).
    /// </para>
    ///
    /// <para>Usage typique dans une activité :</para>
    /// <code>
    /// if (confirmation is null)
    /// {
    ///     var log = ctx.GetVariable&lt;List&lt;CompensationLogEntry&gt;&gt;("CompensationLog")
    ///               ?? [];
    ///     await _compensation.CompensateAsync(log, ctx.SlipId, ct);
    ///     return ActivityResult.Fault(new InvalidOperationException("…"));
    /// }
    /// </code>
    /// </summary>
    public interface IBookingCompensationService
    {
        /// <summary>
        /// Annule toutes les opérations enregistrées dans <paramref name="log"/>,
        /// en ordre inverse (dernière opération annulée en premier).
        /// </summary>
        /// <param name="log">Entrées à compenser — généralement lues depuis Variables["CompensationLog"].</param>
        /// <param name="slipId">Identifiant du slip, utilisé pour la traçabilité des logs.</param>
        /// <param name="ct">Jeton d'annulation.</param>
        /// <returns>
        /// La liste des entrées dont la compensation a <b>échoué</b>.
        /// Une liste vide signifie que toutes les annulations ont réussi.
        /// Le service ne lève jamais d'exception — il gère les échecs en interne
        /// et continue à compenser les autres entrées (best-effort).
        /// </returns>
        Task<IReadOnlyList<CompensationLogEntry>> CompensateAsync(
            IEnumerable<CompensationLogEntry> log,
            string slipId,
            CancellationToken ct);
    }
}
