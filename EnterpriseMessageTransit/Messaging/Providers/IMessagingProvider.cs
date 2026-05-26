using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    public interface IMessagingProvider : IMessageActions
    {
        Task SendAsync<TMessage>(MessageTransitContext<TMessage> context, MessagingOptions options, CancellationToken cancellationToken = default) where TMessage : class;
        Task SendBatchAsync<TMessage>(IEnumerable<MessageTransitContext<TMessage>> contexts, MessagingOptions options, CancellationToken cancellationToken = default) where TMessage : class;
        Task<MessageTransitContext<TMessage>?> RequestReplyAsync<TMessage>(MessageTransitContext<TMessage> context, MessagingOptions options, CancellationToken cancellationToken = default) where TMessage : class;

        /// <summary>
        /// Exposition de la résolution d'endpoint
        /// </summary>
        EndpointSettings Resolve(string? target);

        /// <summary>
        /// Désérialise le message entrant en retournant un résultat structuré.
        /// Utiliser <see cref="DeserializationResult{TMessage}.IsSuccess"/> pour décider du settlement.
        /// </summary>
        DeserializationResult<MessageTransitContext<TMessage>> DeserializeMessageSafe<TMessage>() where TMessage : class;

        /// <summary>
        /// Retourne le <c>traceparent</c> W3C propagé par le producteur dans les
        /// <c>ApplicationProperties</c> du message Service Bus, ou <c>null</c> si absent.
        /// Implémentation par défaut : <c>null</c> (adaptateurs sans transport réel).
        /// </summary>
        string? GetTraceparent() => null;
    }
}

