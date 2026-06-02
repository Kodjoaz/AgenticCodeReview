namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Interface composite (backward-compat) regroupant les 5 responsabilités du provider.
    /// Pour les nouveaux composants, injecter les interfaces fines :
    /// <see cref="IMessagePublisher"/>, <see cref="IMessageReceiver"/>, <see cref="IMessageDeserializer"/>,
    /// <see cref="IMessageSettler"/>, <see cref="IMessagingEndpointResolver"/>.
    /// Le pattern Request/Reply est géré par <c>IRequestReplyClient&lt;TRequest,TResponse&gt;</c>.
    /// </summary>
    public interface IMessagingProvider : IMessagePublisher, IMessageActions, IMessagingEndpointResolver, IMessageDeserializer { }
}

