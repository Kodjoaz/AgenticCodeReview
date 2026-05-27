using Microsoft.Extensions.Logging;
using RAMQ.Samples.Queue.TDF.Integration.Producer.Services;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.TDF.Integration.Frontend.Services;

/// <summary>
/// Implementation of producer messaging orchestration via clean HTTP abstraction.
///
/// Coordinates the sequence of EMT protocol steps with the Producer HTTP endpoint:
/// 1. Calls Producer.PublishInitialTransactionAsync() for tdf.envoi (file transfer)
/// 2. Calls Producer.PublishCorrelationAsync() for tdf.correller (confirmation)
///
/// All EMT protocol details are delegated to the Producer.
/// Frontend is decoupled from Service Bus infrastructure.
/// </summary>
public sealed class TDFProducerMessage : IProducerMessage
{
    private readonly ILogger<TDFProducerMessage> _logger;
    private readonly ITdfProducerHttpClient _producerClient;

    public TDFProducerMessage(
        ILogger<TDFProducerMessage> logger,
        ITdfProducerHttpClient producerClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producerClient = producerClient ?? throw new ArgumentNullException(nameof(producerClient));
    }

    public async Task<TdfTransactionResult> ExecuteTransactionFlowAsync(
        string sessionId,
        string numeroEchange,
        string authToken,
        string correlationId,
        string blobReference,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(sessionId, numeroEchange, authToken, correlationId, blobReference);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            { "SessionId", sessionId },
            { "NumeroEchange", numeroEchange },
            { "CorrelationId", correlationId }
        });

        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Producer messaging orchestration started. SessionId={SessionId}, NumeroEchange={NumeroEchange}",
            sessionId, numeroEchange);

        try
        {
            // ─────────────────────────────────────────────────────────────────────────────
            // Step 1: Call Producer to publish initial transaction (EnvoyerLotFichier)
            // ─────────────────────────────────────────────────────────────────────────────
            var initialRequest = new TdfTransactionRequest(
                SessionId: sessionId,
                CorrelationId: correlationId,
                AuthorizationToken: authToken,
                NumeroEchange: numeroEchange,
                BlobReference: blobReference,
                ContentType: "application/octet-stream",
                SizeBytes: 1024);

            var initialResult = await _producerClient.PublishInitialTransactionAsync(
                initialRequest, cancellationToken);

            _logger.LogInformation(
                "Step 1 (tdf.envoi) completed via Producer. MessageId={MessageId}, BlobRef={BlobRef}",
                initialResult.MessageId, blobReference);

            // ─────────────────────────────────────────────────────────────────────────────
            // Step 2: Call Producer to publish correlation (CorrellerEnvoyer)
            // ─────────────────────────────────────────────────────────────────────────────
            var accuseReception = $"AR-{numeroEchange}";
            var correlationRequest = new TdfCorrelationRequest(
                SessionId: sessionId,
                CorrelationId: correlationId,
                AuthorizationToken: authToken,
                NumeroEchange: numeroEchange,
                AccuseReception: accuseReception);

            var correlationResult = await _producerClient.PublishCorrelationAsync(
                correlationRequest, cancellationToken);

            _logger.LogInformation(
                "Step 2 (tdf.correller) completed via Producer. MessageId={MessageId}, Duration={Duration}ms",
                correlationResult.MessageId, sw.ElapsedMilliseconds);

            return new TdfTransactionResult(
                Success: true,
                SessionId: sessionId,
                InitialMessageId: initialResult.MessageId,
                CorrelationMessageId: correlationResult.MessageId,
                CompletedAt: DateTime.UtcNow,
                Duration: sw.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Producer messaging orchestration failed. SessionId={SessionId}, Duration={Duration}ms",
                sessionId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private static void ValidateInputs(
        string sessionId,
        string numeroEchange,
        string authToken,
        string correlationId,
        string blobReference)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId is required", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(numeroEchange))
            throw new ArgumentException("NumeroEchange is required", nameof(numeroEchange));
        if (string.IsNullOrWhiteSpace(authToken))
            throw new ArgumentException("AuthToken is required", nameof(authToken));
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId is required", nameof(correlationId));
        if (string.IsNullOrWhiteSpace(blobReference))
            throw new ArgumentException("BlobReference is required", nameof(blobReference));
    }
}

