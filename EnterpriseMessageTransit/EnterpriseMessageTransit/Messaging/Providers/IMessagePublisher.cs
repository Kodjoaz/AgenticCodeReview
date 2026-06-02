using RAMQ.COM.EnterpriseMessageTransit.Configuration;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Responsabilité unique : envoi de messages vers Service Bus.
    /// Un émetteur pur (fire-and-forget) ne dépend que de cette interface.
    /// </summary>
    public interface IMessagePublisher
    {
        Task SendAsync<TMessage>(MessageTransitContext<TMessage> context, MessagingOptions options, CancellationToken cancellationToken = default) where TMessage : class;
        Task SendBatchAsync<TMessage>(IEnumerable<MessageTransitContext<TMessage>> contexts, MessagingOptions options, CancellationToken cancellationToken = default) where TMessage : class;
    }
}
