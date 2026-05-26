namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    public interface IMessagingAdapter : IMessageActions
    {
        IMessageTransit GetMessage();

        /// <summary>
        /// Retourne le <c>traceparent</c> W3C lu dans les <c>ApplicationProperties</c> du message
        /// reçu, ou <c>null</c> si absent ou non applicable.
        /// Utilisé par <see cref="Consumer.BaseConsumer{TMessage}"/> pour rétablir la corrélation
        /// cross-service côté consommateur.
        /// </summary>
        string? GetTraceparent() => null;
    }
}
