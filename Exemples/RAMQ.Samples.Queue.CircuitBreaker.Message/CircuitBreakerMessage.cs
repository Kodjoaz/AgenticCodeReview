namespace RAMQ.Samples.Queue.CircuitBreaker.Message
{
    /// <summary>
    /// Message de démonstration du Circuit Breaker.
    /// Publié vers deux cibles : "healthy-queue" (stable) et "failing-queue" (simulée en panne).
    /// </summary>
    public record CircuitBreakerMessage
    {
        public Guid   Id      { get; init; }
        public string Payload { get; init; } = string.Empty;
        public string Target  { get; init; } = string.Empty;
    }
}
