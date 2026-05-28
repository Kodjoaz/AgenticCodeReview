using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;
using RAMQ.Samples.Queue.ClaimCheck.PDF.Message;
using System.Text;

namespace RAMQ.Samples.Queue.ClaimCheck.PDF.Consumer
{
    /// <summary>
    /// Consumer Claim Check — démontre les deux stratégies de consommation d'un message > 256 Ko.
    ///
    /// Option A (référence) : récupère l'URL du blob depuis le token et la transmet à
    ///   l'API downstream. L'API télécharge elle-même depuis Blob Storage.
    ///   Avantage : aucun transfert de données dans le worker ; idéal si l'API est proche du storage.
    ///
    /// Option B (téléchargement inline) : télécharge le contenu via IStorageProvider.DownloadAsync
    ///   et le traite directement dans le consumer.
    ///   Avantage : traitement local complet ; utile si l'API downstream n'a pas accès au storage.
    /// </summary>
    public class ClaimCheckPdfConsumer : BaseConsumer<PdfRapportMessage>
    {
        public ClaimCheckPdfConsumer(
            IMessagingProvider messagingProvider,
            ILogger<ClaimCheckPdfConsumer> logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            string? targetName = null,
            string? consumerName = null,
            string? actionName = null,
            IMetricsProvider? metricsProvider = null)
            : base(messagingProvider, logger, config, serializer, storageProvider, targetName, consumerName, actionName, metricsProvider)
        {
        }

        public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<PdfRapportMessage> context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            using var scope = Logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"]   = context.MessageId,
                ["RapportId"]   = context.Message?.RapportId,
                ["ClaimCheck"]  = context.IsClaimCheckApplied
            });

            var reply = new MessageTransitResponse { StatusCode = 200, Content = "OK" };

            try
            {
                var meta = context.Message!;
                Logger.LogInformation(
                    "Réception rapport {TypeRapport} patient {PatientId} ({TailleOctets} octets) ClaimCheck={ClaimCheck}",
                    meta.TypeRapport, meta.PatientId, meta.TailleOctets, context.IsClaimCheckApplied);

                if (context.IsClaimCheckApplied)
                {
                    var token = context.GetMessageToken();
                    if (token?.Reference is null)
                    {
                        throw new ImmediateDLQException("Claim Check appliqué mais token de référence absent.");
                    }

                    // ── Option A : passer la référence blob à l'API downstream ────────────
                    // L'API télécharge directement depuis Azure Blob Storage.
                    // Recommandé quand l'API est co-localisée avec le storage (même région).
                    Logger.LogInformation(
                        "Option A — référence blob transmise à l'API downstream : {Reference}",
                        token.Reference);
                    await SimulerAppelApiDownstreamOptionA(token.Reference, meta, cancellationToken);

                    // ── Option B : télécharger inline via IStorageProvider ────────────────
                    // Le worker lit le contenu et le traite localement.
                    // Recommandé quand l'API downstream n'a pas accès au storage.
                    Logger.LogInformation(
                        "Option B — téléchargement inline depuis blob : {Reference}",
                        token.Reference);
                    await using var stream = await StorageProvider.DownloadAsync(token.Reference, cancellationToken);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var contenu = await reader.ReadToEndAsync(cancellationToken);
                    Logger.LogInformation(
                        "Option B — {Bytes} octets téléchargés et traités localement.",
                        Encoding.UTF8.GetByteCount(contenu));
                }
                else
                {
                    // Message léger (< seuil) — payload directement dans le contexte.
                    Logger.LogInformation("Message sous le seuil Claim Check — payload inline.");
                }

                await CompleteMessageAsync(cancellationToken);
            }
            catch (ImmediateDLQException ex)
            {
                reply.IsPermanentFailure = true;
                await DeadLetterMessageAsync(ex, cancellationToken);
            }
            catch (ImmediateRetryException ex)
            {
                reply.IsTransient = true;
                await ImmediateRetryAsync(ex, cancellationToken);
            }
            catch (ExponentialRetryException ex)
            {
                reply.IsTransient = true;
                await ExponentialRetryAsync(ex, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Annulation ClaimCheckPdfConsumer MessageId={MessageId}", context.MessageId);
                throw;
            }
            catch (Exception ex)
            {
                reply.IsTransient = true;
                Logger.LogError(ex, "Erreur ClaimCheckPdfConsumer MessageId={MessageId}", context.MessageId);
                await DeadLetterMessageAsync(ex, cancellationToken);
            }

            return context.CopyWithResponse(reply);
        }

        // Simule un appel API downstream avec la référence blob (Option A).
        private Task SimulerAppelApiDownstreamOptionA(
            string blobReference,
            PdfRapportMessage meta,
            CancellationToken cancellationToken)
        {
            // En production : appel HTTP vers l'API qui télécharge le blob elle-même.
            // Ex: await _httpClient.PostAsJsonAsync("/api/rapports", new { BlobRef = blobReference, ... }, ct);
            Logger.LogInformation(
                "API downstream notifiée — RapportId={RapportId} BlobRef={BlobRef}",
                meta.RapportId, blobReference);
            return Task.CompletedTask;
        }
    }
}
