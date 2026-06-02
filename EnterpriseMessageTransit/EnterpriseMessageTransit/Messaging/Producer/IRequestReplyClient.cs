namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Contrat du pattern Request/Reply. Séparé de <see cref="IMessageProducer{TPayload}"/>
    /// pour respecter ISP — un émetteur fire-and-forget n'a pas à dépendre de GetResponseAsync.
    /// </summary>
    /// <typeparam name="TRequest">Type du message requête envoyé au responder.</typeparam>
    /// <typeparam name="TResponse">Type du message réponse attendu du responder.</typeparam>
    public interface IRequestReplyClient<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Envoie une requête sur l'endpoint de requête et attend la réponse sur l'endpoint
        /// de réponse via session Service Bus corrélée par <c>SessionId</c>.
        /// Retourne <c>null</c> si le timeout expire avant réception de la réponse.
        /// </summary>
        Task<MessageTransitContext<TResponse>?> GetResponseAsync(
            MessageTransitContext<TRequest> context,
            RequestReplyOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
