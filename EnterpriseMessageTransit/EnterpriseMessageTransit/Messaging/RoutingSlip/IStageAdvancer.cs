namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Logique pure d'avancement de stage (RoutingSlip 2.0) — aucun I/O, aucune dépendance SDK.
    /// <para>
    /// Injecté optionnellement dans
    /// <see cref="RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer.BaseConsumer{TMessage}"/>
    /// via le constructeur. L'implémentation par défaut est <see cref="StageAdvancer"/>.
    /// </para>
    /// </summary>
    public interface IStageAdvancer
    {
        /// <summary>
        /// Trouve l'index 0-basé du stage dans le slip.
        /// Applique les 3 stratégies de résolution (StageId exact, Target exact, préfixe sans action).
        /// </summary>
        /// <exception cref="InvalidOperationException">Stage introuvable dans l'itinéraire.</exception>
        int FindIndex(RoutingSlip slip, string effectiveStage);

        /// <summary>
        /// Calcule le résultat d'avancement depuis le stage courant.
        /// Détermine si l'étape est finale et identifie le prochain stage.
        /// </summary>
        /// <exception cref="InvalidOperationException">Stage introuvable ou itinéraire vide.</exception>
        RoutingSlipResult Advance(RoutingSlip slip, string currentStage, IReadOnlyDictionary<string, object>? variables = null);
    }
}
