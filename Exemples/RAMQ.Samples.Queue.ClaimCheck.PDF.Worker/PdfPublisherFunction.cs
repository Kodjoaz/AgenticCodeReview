using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.Samples.Queue.ClaimCheck.PDF.Message;
using System.Net;
using System.Text;

namespace RAMQ.Samples.Queue.ClaimCheck.PDF.Worker
{
    /// <summary>
    /// HTTP trigger — publie un rapport PDF via le pattern Claim Check.
    ///
    /// Trois cas de test couverts :
    ///
    /// CAS 1 — Gros message (payload JSON > 256 Ko) :
    ///   GET /api/publish-large-json
    ///   Le contexte sérialisé dépasse le seuil → Claim Check automatique.
    ///   Démontre : ClaimCheckOptions.Auto() — seuil piloté par BlobStorageSetting.ClaimCheckThresholdBytes.
    ///
    /// CAS 2 — Pièce jointe (fichier binaire uploadé) :
    ///   POST /api/publish-with-attachment  (body = contenu binaire du fichier)
    ///   Démontre : ClaimCheckOptions.WithAttachment(stream, fileName) — le fichier est uploadé
    ///   séparément du message métier, indépendamment de la taille du payload.
    ///
    /// CAS 3 — Message léger (payload < seuil, sans pièce jointe) :
    ///   GET /api/publish-light
    ///   Le payload reste inline dans le message Service Bus.
    ///   Démontre : aucun Claim Check appliqué — context.IsClaimCheckApplied = false côté consumer.
    /// </summary>
    public class PdfPublisherFunction
    {
        private readonly ILogger<PdfPublisherFunction> _logger;
        private readonly IMessageProducer<PdfRapportMessage> _producer;

        public PdfPublisherFunction(
            ILogger<PdfPublisherFunction> logger,
            IMessageProducer<PdfRapportMessage> producer)
        {
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        // ─────────────────────────────────────────────────────────────────────
        // CAS 1 — Gros message JSON (> seuil ClaimCheck automatique)
        // ─────────────────────────────────────────────────────────────────────
        [Function("PublishLargeJson")]
        public async Task<HttpResponseData> PublishLargeJsonAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "publish-large-json")]
            HttpRequestData req,
            CancellationToken cancellationToken)
        {
            var rapportId = Guid.NewGuid().ToString("N");

            // Génère un rapport JSON volumineux (> 300 Ko) pour déclencher le Claim Check automatique.
            var donneesVolumineuses = GenererDonneesVolumineuses(rapportId, tailleKo: 350);

            var meta = new PdfRapportMessage
            {
                RapportId    = rapportId,
                PatientId    = "NAM-1234567890",
                TypeRapport  = "IRM-Cerveau",
                FileName     = $"rapport-irm-{rapportId}.json",
                TailleOctets = Encoding.UTF8.GetByteCount(donneesVolumineuses),
                DateRapport  = DateTime.UtcNow
            };

            var ctx = new MessageTransitContext<PdfRapportMessage>
            {
                MessageId     = rapportId,
                CorrelationId = rapportId,
                Message       = meta,
                // Les données volumineuses sont embarquées dans le contexte sérialisé.
                // EMT détecte automatiquement que la taille dépasse le seuil et
                // uploade le payload en Blob avant d'envoyer le message Service Bus.
                Variables     = new Dictionary<string, object>
                {
                    ["donnees"] = donneesVolumineuses
                }
            };

            // ClaimCheckOptions non passé → EMT applique automatiquement si > seuil.
            await _producer.PublishAsync(ctx, null, cancellationToken);

            _logger.LogInformation(
                "CAS 1 — Gros message publié. RapportId={RapportId} Taille~{TailleKo}Ko → Claim Check automatique.",
                rapportId, meta.TailleOctets / 1024);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(
                $"{{\"rapportId\":\"{rapportId}\",\"cas\":\"GrosMessage\",\"tailleKo\":{meta.TailleOctets / 1024}}}",
                cancellationToken);
            return response;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CAS 2 — Pièce jointe binaire uploadée avec le message
        // ─────────────────────────────────────────────────────────────────────
        [Function("PublishWithAttachment")]
        public async Task<HttpResponseData> PublishWithAttachmentAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "publish-with-attachment")]
            HttpRequestData req,
            CancellationToken cancellationToken)
        {
            var rapportId = Guid.NewGuid().ToString("N");
            var fileName  = req.Headers.TryGetValues("X-FileName", out var hv)
                ? hv.First()
                : $"piece-jointe-{rapportId}.pdf";

            // Le body de la requête HTTP est la pièce jointe binaire (PDF, image, etc.).
            using var attachmentStream = new MemoryStream();
            await req.Body.CopyToAsync(attachmentStream, cancellationToken);
            attachmentStream.Position = 0;

            if (attachmentStream.Length == 0)
            {
                // En l'absence de vrai fichier, génère une pièce jointe de test (5 Mo de données aléatoires).
                var fakeData = new byte[5 * 1024 * 1024];
                new Random(42).NextBytes(fakeData);
                await attachmentStream.WriteAsync(fakeData, cancellationToken);
                attachmentStream.Position = 0;
            }

            var meta = new PdfRapportMessage
            {
                RapportId    = rapportId,
                PatientId    = "NAM-9876543210",
                TypeRapport  = "Radio-Thorax",
                FileName     = fileName,
                TailleOctets = attachmentStream.Length,
                DateRapport  = DateTime.UtcNow
            };

            var ctx = new MessageTransitContext<PdfRapportMessage>
            {
                MessageId     = rapportId,
                CorrelationId = rapportId,
                Message       = meta
            };

            // ClaimCheckOptions.WithAttachment : upload la pièce jointe en Blob Storage,
            // indépendamment de la taille du payload JSON du message.
            var options = new PublishOptions
            {
                ClaimCheck = ClaimCheckOptions.WithAttachment(attachmentStream, fileName)
            };

            await _producer.PublishAsync(ctx, options, cancellationToken);

            _logger.LogInformation(
                "CAS 2 — Pièce jointe publiée. RapportId={RapportId} Fichier={FileName} Taille={TailleKo}Ko.",
                rapportId, fileName, attachmentStream.Length / 1024);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(
                $"{{\"rapportId\":\"{rapportId}\",\"cas\":\"PieceJointe\",\"fichier\":\"{fileName}\",\"tailleKo\":{attachmentStream.Length / 1024}}}",
                cancellationToken);
            return response;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CAS 3 — Message léger (< seuil, pas de pièce jointe)
        // ─────────────────────────────────────────────────────────────────────
        [Function("PublishLight")]
        public async Task<HttpResponseData> PublishLightAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "publish-light")]
            HttpRequestData req,
            CancellationToken cancellationToken)
        {
            var rapportId = Guid.NewGuid().ToString("N");

            var meta = new PdfRapportMessage
            {
                RapportId    = rapportId,
                PatientId    = "NAM-1111111111",
                TypeRapport  = "Prise-de-sang",
                FileName     = $"bilan-{rapportId}.json",
                TailleOctets = 1024, // 1 Ko — sous le seuil
                DateRapport  = DateTime.UtcNow
            };

            var ctx = new MessageTransitContext<PdfRapportMessage>
            {
                MessageId     = rapportId,
                CorrelationId = rapportId,
                Message       = meta
            };

            // Aucune option ClaimCheck → payload inline dans le message Service Bus.
            await _producer.PublishAsync(ctx, null, cancellationToken);

            _logger.LogInformation(
                "CAS 3 — Message léger publié inline. RapportId={RapportId} (pas de Claim Check).",
                rapportId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(
                $"{{\"rapportId\":\"{rapportId}\",\"cas\":\"MessageLéger\",\"claimCheck\":false}}",
                cancellationToken);
            return response;
        }

        // Génère un objet JSON volumineux simulant un rapport médical structuré.
        private static string GenererDonneesVolumineuses(string rapportId, int tailleKo)
        {
            var sb = new StringBuilder();
            sb.Append($"{{\"rapportId\":\"{rapportId}\",\"observations\":[");
            int i = 0;
            while (Encoding.UTF8.GetByteCount(sb.ToString()) < tailleKo * 1024)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"{{\"seq\":{i},\"code\":\"OBS-{i:D6}\",\"valeur\":\"{Guid.NewGuid():N}\",\"unite\":\"mg/dL\",\"commentaire\":\"Observation médicale numéro {i} — données structurées pour test de charge EMT Claim Check.\"}}");
                i++;
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}
