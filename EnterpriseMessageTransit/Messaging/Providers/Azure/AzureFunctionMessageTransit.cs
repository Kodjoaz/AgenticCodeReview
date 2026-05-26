using Azure.Messaging.ServiceBus;
using System.Diagnostics.CodeAnalysis;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    [ExcludeFromCodeCoverage]
    public class AzureFunctionMessageTransit : IMessageTransit, IHasRawServiceBusMessage
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

        // Phase 2 (P2-C4) — propriétés enrichies pour éliminer les casts côté consumers
        public int DeliveryCount => _message.DeliveryCount;
        public DateTimeOffset EnqueuedTime => _message.EnqueuedTime;
        public string? CorrelationId => _message.CorrelationId;
        public string? ReplyTo => _message.ReplyTo;
        public IReadOnlyDictionary<string, object> ApplicationProperties
            => _message.ApplicationProperties;

        // Expose the raw ServiceBus message for Azure-specific adapters/providers.
        public ServiceBusReceivedMessage RawMessage => _message;
    }

    // Utilisation typique dans l'adapter
    // public IMessageTransit GetMessage() => new AzureFunctionMessageTransit(_message);
}
