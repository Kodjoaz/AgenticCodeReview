using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.Samples.Queue.TDF.SeqCon.Consumer.Messages;
using System.Diagnostics;
using System.Text;

namespace RAMQ.Samples.Queue.TDF.SeqCon.Worker;

/// <summary>
/// Activateur TDF — Génère une session de transfert de fichiers et publie
/// les deux messages TDF dans la même session Service Bus.
///
/// ════════════════════════════════════════════════════════════════════════
/// Pattern Sequential Convoy — rôle de l'Activateur
/// ════════════════════════════════════════════════════════════════════════
///
/// Étape 1 — EnvoyerLotFichier (step = tdf.envoi)
///   • Téléverse un fichier de test dans Blob Storage (chemin relatif).
///   • Publie TdfTransactionCommand avec TokenMessage(Kind=File).
///   • SessionId = GUID → identifiant de session unique par transaction.
///
/// Étape 2 — CorrellerEnvoyer (step = tdf.correller)
///   • Publie TdfTransactionCommand avec AccuseReception.
///   • MÊME SessionId → Service Bus session garantit l'ordre FIFO des deux messages.
///
/// Garantie Sequential Convoy :
///   Les deux messages partagent le même SessionId.
///   Le Subscriber les traite séquentiellement dans l'ordre d'arrivée.
///   L'orchestrateur Durable Functions (StateFul) corrèle les deux étapes.
///
/// DFO — Observabilité de l'Activateur
///   • BeginScope → SessionId, NumeroEchange, CorrelationId propagés dans chaque log.
///   • enableScopeProperties = true dans host.json → dimensions dans Application Insights.
///   • Stopwatch → Duration publiée à la fin pour alertes SLA.
/// </summary>
public sealed class TdfActivateurFunction
{
    private readonly ILogger<TdfActivateurFunction> _logger;
    private readonly IMessageProducer<TdfTransactionCommand> _producer;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IOptions<BlobStorageSetting> _blobSettings;

    public TdfActivateurFunction(
        ILogger<TdfActivateurFunction> logger,
        IMessageProducer<TdfTransactionCommand> producer,
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageSetting> blobSettings)
    {
        _logger            = logger            ?? throw new ArgumentNullException(nameof(logger));
        _producer          = producer          ?? throw new ArgumentNullException(nameof(producer));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _blobSettings      = blobSettings      ?? throw new ArgumentNullException(nameof(blobSettings));
    }

    [Function(nameof(TdfActivateurFunction))]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
        CancellationToken ct)
    {
        var sessionId     = Guid.NewGuid().ToString("N");
        var numeroEchange = $"ECHANGE-{DateTime.UtcNow:yyyyMMddHHmmss}-{sessionId[..8].ToUpper()}";
        var authToken     = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString("N");

        var sw = Stopwatch.StartNew();

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["SessionId"]     = sessionId,
            ["NumeroEchange"] = numeroEchange,
            ["CorrelationId"] = correlationId
        });

        _logger.LogInformation(
            "Activateur TDF démarré. SessionId={SessionId}, NumeroEchange={NumeroEchange}",
            sessionId, numeroEchange);

        try
        {
            // ── Étape 1 : Téléverser le fichier de test dans Blob Storage ────────────────
            var blobRef = await UploadTestBlobAsync(sessionId, ct);

            // ── Étape 2 : Publier tdf.envoi avec le token de fichier ─────────────────────
            // Le SessionId est la clé de corrélation — le Subscriber l'utilise comme InstanceId
            // de l'orchestration Durable Functions.
            var envoiContext = new MessageTransitContext<TdfTransactionCommand>
            {
                SessionId     = sessionId,
                CorrelationId = correlationId,
                MessageType   = nameof(TdfTransactionCommand),
                Variables     = new Dictionary<string, object> { ["step"] = "tdf.envoi" },
                Tokens        =
                [
                    new TokenMessage
                    {
                        Kind        = TokenKind.File,
                        Reference   = blobRef,      // chemin RELATIF — validé par le Consumer
                        ContentType = "application/octet-stream",
                        SizeBytes   = 1024
                    }
                ],
                Message = new TdfTransactionCommand(
                    AuthorizationToken: authToken,
                    NumeroEchange:      numeroEchange,
                    BlobReference:      blobRef)
            };

            var envoiResult = await _producer.PublishAsync(envoiContext, cancellationToken: ct);

            _logger.LogInformation(
                "tdf.envoi publié. MessageId={MessageId}, BlobRef={BlobRef}",
                envoiResult.MessageId, blobRef);

            // ── Étape 3 : Publier tdf.correller avec l'accusé de réception ───────────────
            // Même SessionId → Service Bus session garantit que ce message est traité
            // APRÈS tdf.envoi dans la même session (Sequential Convoy).
            var accuseReception  = $"AR-{numeroEchange}";
            var correllerContext = new MessageTransitContext<TdfTransactionCommand>
            {
                SessionId     = sessionId,
                CorrelationId = correlationId,
                MessageType   = nameof(TdfTransactionCommand),
                Variables     = new Dictionary<string, object> { ["step"] = "tdf.correller" },
                Message = new TdfTransactionCommand(
                    AuthorizationToken: authToken,
                    NumeroEchange:      numeroEchange,
                    AccuseReception:    accuseReception)
            };

            var correllerResult = await _producer.PublishAsync(correllerContext, cancellationToken: ct);

            _logger.LogInformation(
                "tdf.correller publié. MessageId={MessageId}, AccuseReception={AccuseReception}, Duration={Duration}ms",
                correllerResult.MessageId, accuseReception, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Activateur TDF en erreur. SessionId={SessionId}, Duration={Duration}ms",
                sessionId, sw.ElapsedMilliseconds);
            throw;
        }

        if (myTimer.ScheduleStatus is not null)
            _logger.LogInformation("Prochain déclenchement : {Next}", myTimer.ScheduleStatus.Next);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Téléversement du fichier de test dans Blob Storage.
    // IMPORTANT : retourne un chemin RELATIF (ex: "transmissions/abc123.bin").
    //             Le Consumer valide que Reference ne commence pas par "http".
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<string> UploadTestBlobAsync(string sessionId, CancellationToken ct)
    {
        var settings  = _blobSettings.Value;
        var container = _blobServiceClient.GetBlobContainerClient(settings.ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobName   = $"{settings.FolderName}/{sessionId}.bin";
        var blobClient = container.GetBlobClient(blobName);

        var content = Encoding.UTF8.GetBytes($"TDF-TEST-PAYLOAD|{sessionId}|{DateTime.UtcNow:O}");
        using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        _logger.LogDebug("Blob téléversé : {BlobName} ({Bytes} octets)", blobName, content.Length);
        return blobName;
    }
}
