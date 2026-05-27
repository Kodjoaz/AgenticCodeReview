namespace RAMQ.Samples.Queue.TDF.Integration.Producer.Services;

/// <summary>
/// Service contract for Frontend to communicate with Producer.
/// Abstracts HTTP details and provides clean EMT message publishing interface.
/// </summary>
public interface ITdfProducerService
{
    /// <summary>
    /// Publishes initial TDF transaction (EnvoyerLotFichier — step: tdf.envoi).
    /// Frontend sends file metadata and token; Producer validates, enriches, and publishes.
    /// </summary>
    Task<TdfPublishResult> PublishInitialTransactionAsync(
        TdfTransactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes correlation confirmation (CorrellerEnvoyer — step: tdf.correller).
    /// Uses same SessionId to maintain Sequential Convoy guarantee.
    /// </summary>
    Task<TdfPublishResult> PublishCorrelationAsync(
        TdfCorrelationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record TdfTransactionRequest(
    string SessionId,
    string CorrelationId,
    string AuthorizationToken,
    string NumeroEchange,
    string? BlobReference = null,
    string? ContentType = null,
    long SizeBytes = 0);

public sealed record TdfCorrelationRequest(
    string SessionId,
    string CorrelationId,
    string AuthorizationToken,
    string NumeroEchange,
    string AccuseReception);

public sealed record TdfPublishResult(
    string MessageId,
    string SessionId,
    DateTime PublishedAt);
