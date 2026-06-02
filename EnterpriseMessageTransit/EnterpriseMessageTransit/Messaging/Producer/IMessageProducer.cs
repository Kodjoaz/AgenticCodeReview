namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Abstraction pour la publication de messages (fire-and-forget, single et batch).
    /// Pour le pattern Request/Reply, utiliser <see cref="IRequestReplyClient{TRequest,TResponse}"/>.
    /// </summary>
    public interface IMessageProducer<TPayload> where TPayload : class
    {
        /// <summary>
        /// Publie un message avec PublishOptions (target, propriétés, claim check).
        /// </summary>
        Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(
            MessageTransitContext<TPayload> context,
            PublishOptions? publishOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publie un lot de messages. Retourne la liste ordonnée des MessageId envoyés,
        /// dans le même ordre que la collection <paramref name="contexts"/> fournie.
        /// </summary>
        Task<IReadOnlyList<string>> PublishBatchAsync(
            IEnumerable<MessageTransitContext<TPayload>> contexts,
            PublishOptions? publishOptions = null,
            CancellationToken cancellationToken = default);
    }

}
