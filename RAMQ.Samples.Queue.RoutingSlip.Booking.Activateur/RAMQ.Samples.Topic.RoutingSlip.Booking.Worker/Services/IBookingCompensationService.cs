using RAMQ.Samples.RoutingSlip.Booking.Message;

namespace RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Services
{
    /// <summary>
    /// Contrat du service de compensation pour le workflow de réservation (variante Topic).
    ///
    /// <para>
    /// Même contrat que la variante Queue — seul le namespace diffère.
    /// Le transport (Queue vs Topic) est transparent pour la logique de compensation.
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
