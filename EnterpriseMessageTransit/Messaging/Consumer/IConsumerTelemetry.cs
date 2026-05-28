namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer
{
    internal interface IConsumerTelemetry
    {
        ConsumeScope BeginReceive(string? traceparent);
        void RecordReceived(string entityName, double durationMs);
        void RecordDeserializationFailure(string failureReason);
    }
}
