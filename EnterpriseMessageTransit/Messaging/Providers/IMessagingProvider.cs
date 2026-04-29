using RAMQ.COM.EnterpriseMessageTransit.Configuration;

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
        MessageTransitContext<TMessage>? DeserializeMessage<TMessage>() where TMessage : class;
        bool TryDeserialize<TMessage>(out MessageTransitContext<TMessage>? context) where TMessage : class;
    }
}

