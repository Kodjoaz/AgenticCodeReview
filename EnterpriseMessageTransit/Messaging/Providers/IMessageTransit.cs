namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    public interface IMessageTransit
    {
        string MessageId { get; }
        string Content { get; }
        long SequenceNumber { get; }
        string? SessionId { get; }
    }
}
