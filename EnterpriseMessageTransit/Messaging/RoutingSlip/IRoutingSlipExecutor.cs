using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Chef d'orchestre interne du routing slip.
    /// Enregistré par <c>AddRoutingSlipActivity</c> et appelé depuis le Worker ou l'Azure Function.
    ///
    /// Une instance de ce type = une étape = un type TArgs.
    /// </summary>
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
