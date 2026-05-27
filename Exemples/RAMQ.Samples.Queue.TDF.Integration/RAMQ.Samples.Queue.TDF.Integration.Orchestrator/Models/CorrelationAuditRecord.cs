namespace RAMQ.Samples.Queue.TDF.Integration.StateFul.Models;

/// <summary>
/// Données d'audit passées à <see cref="Activities.RecordAuditActivity"/>.
/// Immuable et JSON-sérialisable (requis pour les Activities Durable Functions).
/// </summary>
public sealed record CorrelationAuditRecord
{
    public required string   InstanceId       { get; init; }
    public required string   SessionId        { get; init; }
    public required string   NumeroEchange    { get; init; }
    public required string   Stage            { get; init; }
    public required string   InitialMsgId     { get; init; }
    public          string?  CorrelMsgId      { get; init; }
    public required DateTime StartedAt        { get; init; }
    public required DateTime EventAt          { get; init; }
    public required double   DurationSeconds  { get; init; }
    public          string?  ErrorCode        { get; init; }
    public          string?  ErrorMessage     { get; init; }
}

