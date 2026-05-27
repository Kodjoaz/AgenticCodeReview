namespace RAMQ.Samples.Queue.TDF.SeqCon.Worker.Services;

/// <summary>
/// Abstraction for Frontend (Worker) to orchestrate TDF transaction flow via Producer.
/// Encapsulates all EMT protocol details and Producer HTTP communication.
///
/// The Frontend is responsible for:
/// 1. Generating session identifiers and authentication tokens
/// 2. Uploading files to Blob Storage (Claim Check)
/// 3. Orchestrating the sequence of calls to Producer
///
/// The abstraction hides:
/// - HTTP communication details (Refit client)
/// - EMT MessageTransitContext construction
/// - Producer endpoint selection
/// - Error handling and retry policies
/// </summary>
public interface ITdfProducerOrchestration
{
    /// <summary>
    /// Orchestrates the complete TDF transaction flow (both steps).
    ///
    /// Step 1 (EnvoyerLotFichier): Upload file and publish initial transaction
    /// Step 2 (CorrellerEnvoyer): Publish correlation confirmation
    ///
    /// All communication with Producer is abstracted away.
    /// </summary>
    Task<TdfTransactionResult> ExecuteTransactionFlowAsync(
        string sessionId,
        string numeroEchange,
        string authToken,
        string correlationId,
        string blobReference,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of complete TDF transaction orchestration.
/// </summary>
public sealed record TdfTransactionResult(
    bool Success,
    string SessionId,
    string InitialMessageId,
    string CorrelationMessageId,
    DateTime CompletedAt,
    TimeSpan Duration);
