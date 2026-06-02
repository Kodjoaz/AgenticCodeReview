using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Chef d'orchestre interne du routing slip.
    /// Enregistré par <c>AddRoutingSlipActivity</c> et appelé depuis le Worker ou l'Azure Function.
    /// Le code applicatif devrait préférer <see cref="BaseRoutingSlipFunction"/> plutôt que
    /// résoudre directement cet exécuteur par clé.
    ///
    /// Une instance de ce type = une étape = un type TArgs.
    /// </summary>
    /// <remarks>
    /// <b>ProcessAsync et ExecuteAsync sont interchangeables</b> — les deux délèguent au même pipeline
    /// interne. La distinction de nom est purement conventionnelle :
    /// <list type="bullet">
    ///   <item><description><c>ProcessAsync</c> — convention Queue (ServiceBusTrigger sur une file)</description></item>
    ///   <item><description><c>ExecuteAsync</c> — convention Topic (ServiceBusTrigger sur un abonnement)</description></item>
    /// </list>
    /// N'utilisez qu'une seule méthode par Worker — le comportement est identique.
    /// </remarks>
    public interface IRoutingSlipExecutor
    {
        /// <summary>
        /// Point d'entrée pour les étapes Queue.
        /// Le <paramref name="provider"/> est déjà lié au message entrant via BindContext.
        /// </summary>
        Task ProcessAsync(IMessagingProvider provider, CancellationToken ct);

        /// <summary>
        /// Point d'entrée pour les étapes Topic.
        /// Même pipeline que ProcessAsync — le nom diffère uniquement pour la lisibilité des Workers.
        /// </summary>
        Task ExecuteAsync(IMessagingProvider provider, CancellationToken ct);
    }
}
