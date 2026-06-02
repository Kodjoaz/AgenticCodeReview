using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer
{
    public interface IMessageConsumer<TMessage> where TMessage : class
    {
        Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<TMessage> context,
            CancellationToken cancellationToken);

        Task<DeserializationResult<MessageTransitContext<TPayload>>> DeserializeMessageAsync<TPayload>(CancellationToken cancellationToken = default) where TPayload : class;

        Task DeadLetterMessageAsync(Exception exception, CancellationToken cancellationToken = default);
    }
}

