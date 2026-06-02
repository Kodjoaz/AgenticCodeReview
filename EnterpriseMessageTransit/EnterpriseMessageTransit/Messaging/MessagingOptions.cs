namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    /// <summary>
    /// DTO interne de transport entre Producer et IMessagingProvider.
    /// Immutable après construction (évite les modifications par le provider après réception).
    /// </summary>
    public record MessagingOptions
    {
        public Dictionary<string, object>? Properties { get; init; }
        public bool EnableSession { get; init; }
        public Stream? FileStream { get; init; }
        public string? OriginalFileName { get; init; }
        public bool ForceClaimCheck { get; init; }
        public string? Target { get; init; }
    }
}
