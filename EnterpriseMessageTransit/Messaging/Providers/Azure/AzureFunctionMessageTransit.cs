using Azure.Messaging.ServiceBus;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    public class AzureFunctionMessageTransit : IMessageTransit
    {
        private readonly ServiceBusReceivedMessage _message;

        public AzureFunctionMessageTransit(ServiceBusReceivedMessage message)
        {
            _message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string MessageId => _message.MessageId;
        public string Content => _message.Body.ToString();
        public long SequenceNumber => _message.SequenceNumber;
        public string? SessionId => _message.SessionId;

        // Expose the raw ServiceBus message for Azure-specific adapters/providers.
        public ServiceBusReceivedMessage RawMessage => _message;
    }

    // Utilisation typique dans l'adapter
    // public IMessageTransit GetMessage() => new AzureFunctionMessageTransit(_message);
}
