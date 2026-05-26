using Azure.Messaging.ServiceBus;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    /// <summary>
    /// Interface interne permettant aux adapters Azure d'accéder au message brut Service Bus
    /// sans coupler le code applicatif à <see cref="AzureFunctionMessageTransit"/>.
    /// <para>
    /// P2-C4 — élimine le cast <c>is AzureFunctionMessageTransit</c> dans
    /// <see cref="AzureFunctionMessagingAdapter.BindContext(IMessageTransit, object)"/>.
    /// </para>
    /// </summary>
    internal interface IHasRawServiceBusMessage
    {
        ServiceBusReceivedMessage RawMessage { get; }
    }
}
