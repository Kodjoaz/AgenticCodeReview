using RAMQ.COM.EnterpriseMessageTransit.Messaging;

namespace RAMQ.Samples.Queue.HOA5.Consumer.Messages;

/// <summary>
/// Message reçu sur la file de résultats de l'orchestration TDF.
///
/// Ce record est une copie locale de TransactionCorrelationResult (StateFul).
/// HOA5.Consumer ne référence PAS StateFul pour éviter une dépendance circulaire.
/// La désérialisation JSON s'effectue par correspondance de noms de propriétés.
/// </summary>
public sealed record CorrelationResultMessage
{
    public required string AuthorizationToken { get; init; }
    public string? BlobReference { get; init; }
    public required string NumeroEchange { get; init; }
    public List<TokenMessage>? FileTokens { get; init; }
}
