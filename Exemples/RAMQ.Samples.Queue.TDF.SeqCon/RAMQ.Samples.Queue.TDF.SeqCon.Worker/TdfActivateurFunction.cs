using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.Samples.Queue.TDF.SeqCon.Worker.Services;
using System.Diagnostics;
using System.Text;

namespace RAMQ.Samples.Queue.TDF.SeqCon.Worker;

/// <summary>
/// TDF Frontend Function — Orchestrates file transfer and transaction flow.
///
/// ════════════════════════════════════════════════════════════════════════
/// Architecture: Frontend → Producer → Service Bus
/// ════════════════════════════════════════════════════════════════════════
///
/// Responsibilities:
/// 1. Generate session identifiers and authentication tokens (InitierEnvoi — local)
/// 2. Upload file to Blob Storage (Claim Check pattern)
/// 3. Orchestrate calls to Producer via clean abstraction (ITdfProducerOrchestration)
///
/// The Producer handles:
/// - Validation and Enrichment (VE)
/// - Publishing to Service Bus with EMT protocol
/// - Message Transit Journal and Application Insights integration
///
/// This separation allows Frontend to focus on business logic without knowledge
/// of Service Bus or EMT protocol details.
/// </summary>
public sealed class TdfActivateurFunction
{
    private readonly ILogger<TdfActivateurFunction> _logger;
    private readonly ITdfProducerOrchestration _producerOrchestration;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IOptions<BlobStorageSetting> _blobSettings;

    public TdfActivateurFunction(
        ILogger<TdfActivateurFunction> logger,
        ITdfProducerOrchestration producerOrchestration,
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageSetting> blobSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producerOrchestration = producerOrchestration ?? throw new ArgumentNullException(nameof(producerOrchestration));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _blobSettings = blobSettings ?? throw new ArgumentNullException(nameof(blobSettings));
    }

    [Function(nameof(TdfActivateurFunction))]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
        CancellationToken ct)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var numeroEchange = $"ECHANGE-{DateTime.UtcNow:yyyyMMddHHmmss}-{sessionId[..8].ToUpper()}";
        var authToken = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString("N");

        var sw = Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["SessionId"] = sessionId,
            ["NumeroEchange"] = numeroEchange,
            ["CorrelationId"] = correlationId
        });

        _logger.LogInformation(
            "Frontend TDF cycle démarré. SessionId={SessionId}, NumeroEchange={NumeroEchange}",
            sessionId, numeroEchange);

        try
        {
            // ── Étape 0 : InitierEnvoi — Générer les identifiants (local) ────────────────
            _logger.LogDebug(
                "Step 0 (InitierEnvoi) — identifiers generated locally. SessionId={SessionId}, AuthToken={Token}",
                sessionId, authToken[..8]);

            // ── Étape 1-2 : Téléverser le fichier et orchestrer via Producer ───────────────
            var blobRef = await UploadTestBlobAsync(sessionId, ct);
            _logger.LogDebug("File uploaded to Blob Storage: {BlobRef}", blobRef);

            // ── Via abstraction : appeler Producer pour Steps 1 & 2 ─────────────────────────
            var result = await _producerOrchestration.ExecuteTransactionFlowAsync(
                sessionId: sessionId,
                numeroEchange: numeroEchange,
                authToken: authToken,
                correlationId: correlationId,
                blobReference: blobRef,
                cancellationToken: ct);

            _logger.LogInformation(
                "Frontend cycle completed successfully. Initial={Initial}, Correlation={Correlation}, Duration={Duration}ms",
                result.InitialMessageId, result.CorrelationMessageId, result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Frontend TDF cycle error. SessionId={SessionId}, Duration={Duration}ms",
                sessionId, sw.ElapsedMilliseconds);
            throw;
        }

        if (myTimer.ScheduleStatus is not null)
            _logger.LogInformation("Next trigger: {Next}", myTimer.ScheduleStatus.Next);
    }

    private async Task<string> UploadTestBlobAsync(string sessionId, CancellationToken ct)
    {
        var settings = _blobSettings.Value;
        var container = _blobServiceClient.GetBlobContainerClient(settings.ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobName = $"{settings.FolderName}/{sessionId}.bin";
        var blobClient = container.GetBlobClient(blobName);

        var content = Encoding.UTF8.GetBytes($"TDF-TEST-PAYLOAD|{sessionId}|{DateTime.UtcNow:O}");
        using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        _logger.LogDebug("Blob uploaded: {BlobName} ({Bytes} bytes)", blobName, content.Length);
        return blobName;
    }
}
