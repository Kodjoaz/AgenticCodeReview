using Azure.Messaging.ServiceBus;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Abstraction pour la gestion des stratégies de retry (immédiat, exponentiel).
    /// Encapsule la logique de calcul de délai, schedule, journalisation, et DLQ.
    /// Permet une meilleure séparation des responsabilités : l'adapter gère le binding Service Bus,
    /// ce handler gère la stratégie de retry.
    /// </summary>
    public interface IRetryPolicyHandler
    {
        /// <summary>
        /// Définir les métadonnées d'invocation (target, consumer, action).
        /// </summary>
        void SetMetadata(string? target, string? consumer, string? action);

        /// <summary>
        /// Traite un retry immédiat : abandon ou DLQ selon MaxDeliveryCount.
        /// </summary>
        Task HandleImmediateRetryAsync(
            ServiceBusReceivedMessage message,
            object actions,
            ImmediateRetryException exception,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Traite un retry exponentiel : différencie session (Task.Delay + Abandon) vs non-session (ScheduleMessage + Complete).
        /// </summary>
        Task HandleExponentialRetryAsync(
            ServiceBusReceivedMessage message,
            object actions,
            ExponentialRetryException exception,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Envoie un message en Dead Letter avec contextualisation.
        /// </summary>
        Task HandleDeadLetterAsync(
            ServiceBusReceivedMessage message,
            object actions,
            Exception exception,
            CancellationToken cancellationToken = default);
    }
}
