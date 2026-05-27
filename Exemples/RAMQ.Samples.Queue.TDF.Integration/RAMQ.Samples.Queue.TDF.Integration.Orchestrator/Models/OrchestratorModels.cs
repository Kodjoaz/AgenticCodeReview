using RAMQ.COM.EnterpriseMessageTransit.Messaging;

namespace RAMQ.Samples.Queue.TDF.Integration.StateFul.Models;

/// <summary>
/// Événement initial déclenché par le Subscriber lors de l'Étape 2 (tdf.envoi).
/// </summary>
public record EnvoyerLotFichierEvent
{
    public required string SessionId         { get; init; }
    public required string NumeroEchange     { get; init; }
    public required string AuthorizationToken { get; init; }
    public string?         BlobReference     { get; init; }
    public List<TokenMessage>? FileTokens    { get; init; }
    public required string MessageId         { get; init; }
}

/// <summary>
/// Événement externe levé lors de l'Étape 3 (tdf.correller) pour réveiller l'orchestrateur.
/// </summary>
public record CorrellerEnvoyerEvent
{
    public required string AuthorizationToken { get; init; }
    public required string MessageId          { get; init; }
}

/// <summary>
/// Résultat final retourné par l'orchestrateur après corrélation réussie.
/// </summary>
public record TransactionCorrelationResult
{
    public required string AuthorizationToken { get; init; }
    public string?         BlobReference     { get; init; }
    public required string NumeroEchange     { get; init; }
    public List<TokenMessage>? FileTokens    { get; init; }
}

