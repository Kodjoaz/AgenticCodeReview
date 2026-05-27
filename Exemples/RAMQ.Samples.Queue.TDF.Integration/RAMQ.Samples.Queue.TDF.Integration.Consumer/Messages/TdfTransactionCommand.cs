namespace RAMQ.Samples.Queue.TDF.Integration.Consumer.Messages;

/// <summary>
/// Commande transactionnelle TDF — contrat partagé entre tous les projets du PoC.
/// </summary>
public sealed record TdfTransactionCommand(
    string  AuthorizationToken,
    string  NumeroEchange,
    string? BlobReference   = null,
    string? AccuseReception = null);

