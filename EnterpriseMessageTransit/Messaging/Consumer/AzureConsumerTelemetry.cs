using System.Diagnostics;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer
{
    internal sealed class AzureConsumerTelemetry : IConsumerTelemetry
    {
        private readonly IMetricsProvider? _metrics;

        internal AzureConsumerTelemetry(IMetricsProvider? metrics = null) => _metrics = metrics;

        public ConsumeScope BeginReceive(string? traceparent)
        {
            var consumeActivity = MessagingActivitySource.Source.StartActivity(
                "messaging.consume",
                ActivityKind.Consumer,
                parentId: traceparent);
            consumeActivity?.SetTag("messaging.system", "servicebus");
            consumeActivity?.SetTag("messaging.source.traceparent", traceparent);

            var deserializeActivity = MessagingActivitySource.Source.StartActivity(
                "messaging.deserialize",
                ActivityKind.Consumer);

            return new ConsumeScope(consumeActivity, deserializeActivity);
        }

        public void RecordReceived(string entityName, double durationMs)
        {
            _metrics?.IncrementMessagesReceived(entityName, "Consumer");
            _metrics?.RecordReceiveDuration(durationMs, entityName);
        }

        public void RecordDeserializationFailure(string failureReason) =>
            _metrics?.IncrementDeserializationFailure(failureReason);
    }
}
