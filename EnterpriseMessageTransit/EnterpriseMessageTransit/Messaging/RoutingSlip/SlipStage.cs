namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Représente une étape résolue dans l'itinéraire d'un <see cref="RoutingSlip"/>.
    /// </summary>
    /// <param name="Target">Nom cible brut (depuis <c>EndpointSettings.Target</c>).</param>
    /// <param name="StageId">
    /// Identifiant normalisé de l'étape.
    /// Vaut <c>Consumer[.Action]</c> pour un topic, ou <c>Target</c> pour une queue.
    /// </param>
    public sealed record SlipStage(string Target, string StageId);
}
