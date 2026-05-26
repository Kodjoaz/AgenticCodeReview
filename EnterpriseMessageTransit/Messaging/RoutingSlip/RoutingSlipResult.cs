namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Résultat de l'avancement d'un <see cref="RoutingSlip"/> calculé par <see cref="IStageAdvancer"/>.
    /// Record immuable — pas d'effet de bord.
    /// </summary>
    /// <param name="CurrentStage">Identifiant du stage courant tel que résolu.</param>
    /// <param name="CurrentIndex">Index 0-basé de l'étape courante dans le slip.</param>
    /// <param name="NextStage">Identifiant du stage suivant, ou <c>null</c> si l'étape est finale.</param>
    /// <param name="IsFinal"><c>true</c> lorsque cette étape est la dernière de l'itinéraire.</param>
    /// <param name="Variables">Variables propagées depuis le message courant (snapshot en lecture seule).</param>
    public sealed record RoutingSlipResult(
        string CurrentStage,
        int CurrentIndex,
        string? NextStage,
        bool IsFinal,
        IReadOnlyDictionary<string, object>? Variables);
}
