namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer
{
    public interface IMessageConsumer<TMessage> where TMessage : class
    {
        Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<TMessage> context,
            CancellationToken cancellationToken);

        bool TryDeserializeMessage<TPayload>(out MessageTransitContext<TPayload>? context) where TPayload : class;

        Task DeadLetterMessageAsync(Exception exception, CancellationToken cancellationToken = default);
    }
}

