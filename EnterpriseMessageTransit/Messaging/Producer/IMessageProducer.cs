namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Abstraction alignée sur BaseProducer pour publication (publish) et request/reply.
    /// </summary>
    public interface IMessageProducer<TPayload> where TPayload : class
    {
        /// <summary>
        /// Publie un message. Le target est résolu via IMessageTargetMap ou mono-audience.
        /// </summary>
        Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(
            MessageTransitContext<TPayload> context,
            Dictionary<string, object>? properties = null,
            ClaimCheckOptions? claimCheckOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publie un message avec PublishOptions (target, propriétés, claim check).
        /// </summary>
        Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(
            MessageTransitContext<TPayload> context,
            PublishOptions? publishOptions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publie un lot de messages. Retourne la liste ordonnée des MessageId envoyés,
        /// dans le même ordre que la collection <paramref name="contexts"/> fournie.
        /// </summary>
        Task<IReadOnlyList<string>> PublishBatchAsync(
            IEnumerable<MessageTransitContext<TPayload>> contexts,
            Dictionary<string, object>? properties = null,
            ClaimCheckOptions? claimCheckOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Request/Reply. Le target est résolu via RequestReplyOptions.Target → IMessageTargetMap → mono-audience.
        /// </summary>
        Task<MessageTransitContext<MessageTransitResponse>?> GetResponseAsync(
            MessageTransitContext<TPayload> context,
            RequestReplyOptions? options,
            CancellationToken cancellationToken = default);
    }

}
