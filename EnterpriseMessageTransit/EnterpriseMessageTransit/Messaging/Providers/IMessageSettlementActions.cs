namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Abstraction des opérations de règlement d'un message (Complete, Abandon, DeadLetter).
    /// Aucune référence aux types du SDK Azure Service Bus — permet de changer d'hôte
    /// (Azure Functions, Worker Service, AKS/KEDA) sans modifier les couches métier.
    /// </summary>
    /// <remarks>Phase 2 (P2-C5) — découplage Functions.</remarks>
    public interface IMessageSettlementActions
    {
        /// <summary>Finalise le message : retire de la file, marque comme traité.</summary>
        Task CompleteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Abandonne le message : le remet en file pour relivraison.
        /// </summary>
        /// <param name="propertiesToModify">Propriétés applicatives à écraser avant relivraison (ex. ReferralCount).</param>
        Task AbandonAsync(
            IDictionary<string, object>? propertiesToModify = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Envoie le message en Dead Letter Queue (DLQ).
        /// </summary>
        /// <param name="reason">Code de raison court (ex. "MaxDeliveryCountExceeded").</param>
        /// <param name="description">Description longue optionnelle (ex. message d'exception).</param>
        Task DeadLetterAsync(
            string reason,
            string? description = null,
            CancellationToken cancellationToken = default);
    }
}
