namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Itinéraire immuable et versionné d'un RoutingSlip.
    /// Construit une seule fois par <see cref="IItineraryPlanner"/> au démarrage,
    /// puis passé en lecture seule à <see cref="IStageAdvancer"/>.
    /// </summary>
    /// <param name="Stages">Étapes résolues, dans l'ordre de parcours.</param>
    /// <param name="Version">
    /// Version de l'enveloppe :
    /// <list type="bullet">
    /// <item><term>1</term><description>Format classique (champs <c>CurrentStage</c> / <c>Variables</c> dans le message).</description></item>
    /// <item><term>2</term><description>Format RoutingSlip sérialisé — prévu pour une prochaine phase (E4).</description></item>
    /// </list>
    /// </param>
    public sealed record RoutingSlip(IReadOnlyList<SlipStage> Stages, int Version = 1)
    {
        /// <summary>Slip vide — utilisé comme valeur sentinelle quand l'itinéraire est absent.</summary>
        public static readonly RoutingSlip Empty = new(Array.Empty<SlipStage>());
    }
}
