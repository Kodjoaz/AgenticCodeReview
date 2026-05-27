using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Services;
using System.Diagnostics;
using System.Text;

namespace RAMQ.Samples.Queue.TDF.Integration.Frontend;

public sealed class TdfBackgroundService : BackgroundService
{
    private readonly ILogger<TdfBackgroundService> _logger;
    private readonly ITdfProducerOrchestration _producerOrchestration;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IOptions<BlobStorageSetting> _blobSettings;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public TdfBackgroundService(
        ILogger<TdfBackgroundService> logger,
        ITdfProducerOrchestration producerOrchestration,
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageSetting> blobSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producerOrchestration = producerOrchestration ?? throw new ArgumentNullException(nameof(producerOrchestration));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _blobSettings = blobSettings ?? throw new ArgumentNullException(nameof(blobSettings));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TDF Background Service started");

        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TDF Background Service stopped");
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task ExecuteCycleAsync(CancellationToken ct)
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
            "Frontend TDF cycle started. SessionId={SessionId}, NumeroEchange={NumeroEchange}",
            sessionId, numeroEchange);

        try
        {
            _logger.LogDebug(
                "Step 0 (InitierEnvoi) — identifiers generated locally. SessionId={SessionId}, AuthToken={Token}",
                sessionId, authToken[..8]);

            var blobRef = await UploadTestBlobAsync(sessionId, ct);
            _logger.LogDebug("File uploaded to Blob Storage: {BlobRef}", blobRef);

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
        }
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


