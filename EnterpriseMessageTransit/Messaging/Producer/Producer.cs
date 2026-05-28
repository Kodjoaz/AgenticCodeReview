using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Producer multi Target permettant de publier des messages de différents types spécifiques dans différentes entités.
    /// </summary>
    /// <typeparam name="TMessage">Représente le contenu de l'événement</typeparam>
    public class Producer<TMessage> : BaseMessageTransit<TMessage>, IMessageProducer<TMessage>, IProducerPatterns where TMessage : class
    {
        private readonly IMessagePublisher _publisher;
        private readonly IMessagingEndpointResolver _resolver;
        private readonly IJournalProvider _journal;
        private readonly ISystemClock _systemClock;
        private readonly IMessageTargetMap? _targetMap;
        private readonly IMetricsProvider? _metrics;

        public Producer(
            IMessagePublisher publisher,
            IMessagingEndpointResolver resolver,
            IJournalProvider journal,
            ILogger<Producer<TMessage>> logger,
            IProducerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            ISystemClock systemClock,
            IMessageTargetMap? targetMap = null,
            IMetricsProvider? metrics = null)
            : base(logger, config, serializer, storageProvider)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _resolver  = resolver  ?? throw new ArgumentNullException(nameof(resolver));
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
            _targetMap = targetMap;
            _metrics = metrics;
        }

        public async Task PrepareClaimCheckAsync<TAnyMessage>(
            MessageTransitContext<TAnyMessage> context,
            Stream? fileStream,
            string? originalFileName,
            bool forceClaimcheck,
            CancellationToken cancellationToken)
            where TAnyMessage : class
        {
            await PrepareContextWithTokensAsync(context, fileStream, originalFileName, forceClaimcheck, cancellationToken);
        }

        /// <summary>
        /// Publie un message avec résolution automatique du target.
        /// Chaîne de résolution : PublishOptions.Target → IMessageTargetMap → mono-audience.
        /// </summary>
        public virtual async Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(
            MessageTransitContext<TMessage> context,
            PublishOptions? publishOptions,
            CancellationToken cancellationToken = default)
        {
            return await PublishCoreAsync(
                context,
                publishOptions?.Properties,
                publishOptions?.ClaimCheck,
                cancellationToken);
        }

        private async Task<MessageTransitContext<MessageTransitResponse>> PublishCoreAsync(
            MessageTransitContext<TMessage> context,
            Dictionary<string, object>? properties,
            ClaimCheckOptions? claimCheckOptions,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ValidateRoutingProperties(properties);
            cancellationToken.ThrowIfCancellationRequested();

            claimCheckOptions ??= ClaimCheckOptions.None;

            if (string.IsNullOrWhiteSpace(context.MessageId))
                context.MessageId = Guid.NewGuid().ToString("N");

            // CorrelationId is immutable: set once at publish time to the original MessageId.
            if (string.IsNullOrWhiteSpace(context.CorrelationId))
                context.CorrelationId = context.MessageId;

            // Résolution du target : IMessageTargetMap (DI) → fallback mono-audience.
            string? effectiveTarget = _targetMap?.ResolveTarget<TMessage>();
            EndpointSettings audience = _resolver.Resolve(effectiveTarget);
            string? resolvedTarget = audience.Target;
            bool enableSession = audience.Endpoint.EnableSession;

            // P3-T3 — Timeout borné.
            var publishTimeout = audience.Endpoint.PublishTimeout;
            using var timeoutCts = publishTimeout > TimeSpan.Zero
                ? new CancellationTokenSource(publishTimeout)
                : null;
            using var linkedCts = timeoutCts is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                : null;
            var effectiveCt = linkedCts?.Token ?? cancellationToken;

            if (enableSession && string.IsNullOrWhiteSpace(context.SessionId))
            {
                throw new ArgumentNullException(
                    $"Le paramètre <{nameof(context.SessionId)}> est obligatoire lorsque {nameof(enableSession)} est à true.");
            }

            // Architecture cible : le Producer ne renseigne plus Consumer/Action.
            // Les propriétés doivent déjà être présentes dans le contexte (préparées par le RoutingSlipBuilder/Executor).

            await PrepareClaimCheckAsync(
                context,
                claimCheckOptions.FileStream,
                claimCheckOptions.OriginalFileName,
                claimCheckOptions.ForceClaimCheck,
                effectiveCt);

            var options = new MessagingOptions
            {
                Properties    = properties,
                EnableSession = enableSession,
                FileStream    = claimCheckOptions.FileStream,
                OriginalFileName = claimCheckOptions.OriginalFileName,
                ForceClaimCheck  = claimCheckOptions.ForceClaimCheck,
                Target        = resolvedTarget
            };

            (string? consumer, string? action) = ExtractMessageProperties(properties);

            try
            {
                using var activity = MessagingActivitySource.Source.StartActivity(
                    "messaging.publish",
                    ActivityKind.Producer);

                activity?.SetTag("messaging.system",      "servicebus");
                activity?.SetTag("messaging.destination", resolvedTarget);
                activity?.SetTag("messaging.message_id",  context.MessageId);
                activity?.SetTag("messaging.session_id",  context.SessionId);
                activity?.SetTag("messaging.claimcheck",  context.IsClaimCheckApplied);

                try
                {
                    await _publisher.SendAsync(context, options, effectiveCt);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception sendEx)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, sendEx.Message);
                    activity?.SetTag("exception.type",    sendEx.GetType().FullName);
                    activity?.SetTag("exception.message", sendEx.Message);
                    throw;
                }

                var journalEntry = JournalEntry.ForPublish(
                    consumer ?? "(none)",
                    action ?? "(none)",
                    context.MessageId    ?? string.Empty,
                    context.CorrelationId ?? string.Empty,
                    resolvedTarget ?? string.Empty,
                    context.SessionId,
                    Config.AppSettings?.ApplicationName,
                    _systemClock.UtcNow.UtcDateTime);

                var journalSw = System.Diagnostics.Stopwatch.StartNew();
                try { await _journal.WriteRecordAsync(journalEntry, effectiveCt); journalSw.Stop(); _metrics?.RecordJournalWriteDuration(journalSw.Elapsed.TotalMilliseconds); }
                catch (Exception jEx) { Logger.LogWarning(jEx, "Journal failed (publish) — message sent but not journalized. MessageId={MessageId}", context.MessageId); }

                return MapToResponseContext(context, null);
            }
            catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
            {
                // P3-T3 — Timeout interne déclenché : convertir en MessageSendException
                throw new MessageSendException(
                    $"PublishAsync timed out after {publishTimeout.TotalSeconds:F0}s for target='{resolvedTarget}'.",
                    new TimeoutException($"Operation exceeded {publishTimeout.TotalSeconds:F0}s timeout."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (context.IsClaimCheckApplied)
                {
                    var blobRef = context.GetMessageToken()?.Reference;
                    if (!string.IsNullOrWhiteSpace(blobRef))
                    {
                        try
                        {
                            await StorageProvider.DeleteAsync(blobRef, CancellationToken.None);
                            Logger.LogInformation(
                                "Claim-check orphan compensated: blob '{BlobRef}' deleted after send failure. MessageId={MessageId}",
                                blobRef, context.MessageId);
                        }
                        catch (Exception cleanupEx)
                        {
                            Logger.LogWarning(cleanupEx,
                                "Claim-check orphan: blob '{BlobRef}' could not be cleaned up. MessageId={MessageId}",
                                blobRef, context.MessageId);
                        }
                    }
                }
                throw new MessageSendException($"Send failed for target='{resolvedTarget}': {ex.Message}", ex);
            }
        }

        public virtual async Task<IReadOnlyList<string>> PublishBatchAsync(
            IEnumerable<MessageTransitContext<TMessage>> contexts,
            PublishOptions? publishOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));

            var properties        = publishOptions?.Properties;
            var claimCheckOptions = publishOptions?.ClaimCheck;

            ValidateRoutingProperties(properties);
            cancellationToken.ThrowIfCancellationRequested();

            // Claim-Check interdit en batch : incompatible avec l'atomicité.
            // PublishBatchAsync garantit que soit tous les messages sont envoyés, soit aucun.
            // Si un upload Blob réussit et que l'envoi Service Bus échoue ensuite, les blobs
            // deviennent orphelins — et la compensation N-blobs n'est pas fiable.
            // Solution : utiliser PublishAsync en boucle si le Claim-Check est requis par message.
            if (claimCheckOptions != null
                && (claimCheckOptions.ForceClaimCheck || claimCheckOptions.FileStream != null))
            {
                throw new NotSupportedException(
                    "PublishBatchAsync ne supporte pas le pattern Claim-Check. " +
                    "Le Claim-Check est incompatible avec l'atomicité du batch : un upload Blob réussi " +
                    "suivi d'un échec Service Bus produirait des blobs orphelins non compensables. " +
                    "Utiliser PublishAsync en boucle pour les messages nécessitant le Claim-Check.");
            }

            var contextsList = contexts.ToList();

            // Backpressure : rejeter les batches trop volumineux pour éviter surcharge mémoire.
            int maxBatchSize = Config.AppSettings?.MaxBatchSize ?? 0;
            if (maxBatchSize > 0 && contextsList.Count > maxBatchSize)
            {
                throw new ArgumentException(
                    $"Batch size ({contextsList.Count}) exceeds MaxBatchSize ({maxBatchSize}). " +
                    $"Split the collection into smaller batches or increase AppSettings.MaxBatchSize.");
            }

            foreach (var ctx in contextsList)
            {
                if (string.IsNullOrWhiteSpace(ctx.MessageId))
                    ctx.MessageId = Guid.NewGuid().ToString("N");

                // CorrelationId is immutable: set once at publish time.
                if (string.IsNullOrWhiteSpace(ctx.CorrelationId))
                    ctx.CorrelationId = ctx.MessageId;
            }

            // Résolution du target et activation des sessions (identique à PublishCoreAsync)
            string? effectiveTarget = _targetMap?.ResolveTarget<TMessage>();
            EndpointSettings audience = _resolver.Resolve(effectiveTarget);
            string? resolvedTarget = audience.Target;
            bool enableSession = audience.Endpoint.EnableSession;

            // P3-T3 — Timeout borné sur le batch complet
            var publishTimeout = audience.Endpoint.PublishTimeout;
            using var timeoutCts = publishTimeout > TimeSpan.Zero
                ? new CancellationTokenSource(publishTimeout)
                : null;
            using var linkedCts = timeoutCts is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                : null;
            var effectiveCt = linkedCts?.Token ?? cancellationToken;

            // Validation SessionId si sessions activées sur l'entité
            if (enableSession)
            {
                var missingSession = contextsList.FirstOrDefault(ctx => string.IsNullOrWhiteSpace(ctx.SessionId));
                if (missingSession != null)
                {
                    throw new ArgumentException(
                        $"SessionId obligatoire pour chaque message lorsque la session est activée sur l'entité '{audience.Endpoint.EntityName}'. " +
                        $"MessageId='{missingSession.MessageId}' n'a pas de SessionId.");
                }
            }

            var options = new MessagingOptions
            {
                Properties    = properties,
                EnableSession = enableSession,
                Target        = resolvedTarget
            };

            try
            {
                await _publisher.SendBatchAsync(contextsList, options, effectiveCt);

                // R6 — Journal batch via TransactionalBatch Azure Table (1 transaction par partition ≤ 100).
                // Pattern A5 maintenu : les erreurs de journal ne propagent jamais vers le caller.
                (string? consumer, string? action) = ExtractMessageProperties(properties);
                var journalEntries = contextsList.Select(ctx => JournalEntry.ForPublish(
                    consumer ?? "(none)",
                    action   ?? "(none)",
                    ctx.MessageId    ?? string.Empty,
                    ctx.CorrelationId ?? string.Empty,
                    resolvedTarget   ?? string.Empty,
                    ctx.SessionId,
                    Config.AppSettings?.ApplicationName,
                    _systemClock.UtcNow.UtcDateTime)).ToList();

                var journalBatchSw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    await _journal.WriteBatchAsync(journalEntries, effectiveCt);
                    journalBatchSw.Stop();
                    _metrics?.RecordJournalWriteDuration(journalBatchSw.Elapsed.TotalMilliseconds);
                }
                catch (Exception jEx)
                {
                    Logger.LogWarning(jEx,
                        "Journal batch failed — {Count} messages sent but not journalized.",
                        contextsList.Count);
                }
            }
            catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
            {
                // P3-T3 — Timeout batch interne
                throw new MessageSendException(
                    $"PublishBatchAsync timed out after {publishTimeout.TotalSeconds:F0}s for target='{resolvedTarget}'.",
                    new TimeoutException($"Batch operation exceeded {publishTimeout.TotalSeconds:F0}s timeout."));
            }
            catch (OperationCanceledException) { throw; }
            catch (ArgumentException) { throw; }
            catch (Exception ex)
            {
                throw new MessageSendException($"Batch publish failed: {ex.Message}", ex);
            }

            // Returns MessageIds in the same order as the input collection.
            return contextsList.Select(ctx => ctx.MessageId!).ToList();
        }

        protected MessageTransitContext<MessageTransitResponse> MapToResponseContext<TAnyMessage>(
            MessageTransitContext<TAnyMessage> source,
            MessageTransitResponse? response)
            where TAnyMessage : class
        {
            return source.CopyWithResponse(response);
        }

        public string GetTargetEntityName(string? target = null)
        {
            string? effectiveTarget = target ?? _targetMap?.ResolveTarget<TMessage>();
            var aud = _resolver.Resolve(effectiveTarget);
            if (aud.Endpoint?.EntityName == null)
                throw new InvalidOperationException("EntityName not resolved.");
            return aud.Endpoint.EntityName;
        }

        private static void ValidateRoutingProperties(Dictionary<string, object>? properties)
        {
            if (properties == null || properties.Count == 0) return;

            var invalidKeys = properties.Keys
                .Where(k => !string.Equals(k, MessagePropertyKeys.Consumer, StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(k, MessagePropertyKeys.Action,   StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (invalidKeys.Count > 0)
            {
                throw new ConfigurationException(
                    $"Métadonnées de routage invalides. Clés non autorisées : [{string.Join(", ", invalidKeys)}]. " +
                    "Seules les clés 'Consumer' et 'Action' sont acceptées.");
            }
        }

        protected (string? Consumer, string? Action) ExtractMessageProperties(Dictionary<string, object>? properties)
        {
            string? consumer = null;
            string? action   = null;
            if (properties != null)
            {
                if (properties.TryGetValue(MessagePropertyKeys.Consumer, out var c) && c is string cs) consumer = cs;
                if (properties.TryGetValue(MessagePropertyKeys.Action,   out var a) && a is string ac) action   = ac;
            }
            return (consumer, action);
        }

        protected async Task PrepareContextWithTokensAsync<TAnyMessage>(
            MessageTransitContext<TAnyMessage> context,
            Stream? fileStream,
            string? originalFileName,
            bool forceClaimcheck,
            CancellationToken cancellationToken = default)
            where TAnyMessage : class
        {
            context.Tokens ??= new List<TokenMessage>();

            // Serialize once and reuse the result to avoid double work.
            string serialized = Serializer.Serialize(context);
            long size = System.Text.Encoding.UTF8.GetByteCount(serialized);

            if (RequiresClaimCheck((int)size, forceClaimcheck))
            {
                string? fileName = string.IsNullOrWhiteSpace(context.MessageId)
                    ? $"{Guid.NewGuid():N}.json"
                    : $"{context.MessageId}.json";

                using var uploadActivity = MessagingActivitySource.Source.StartActivity(
                    "messaging.claimcheck.upload",
                    ActivityKind.Internal);
                uploadActivity?.SetTag("messaging.message_id",            context.MessageId);
                uploadActivity?.SetTag("messaging.claimcheck.blob",       fileName);
                uploadActivity?.SetTag("messaging.claimcheck.size_bytes", size);

                string? url;
                var uploadSw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    url = await StorageProvider.UploadAsync(serialized, fileName, cancellationToken);
                    uploadSw.Stop();
                    uploadActivity?.SetStatus(ActivityStatusCode.Ok);
                    _metrics?.RecordClaimCheckUploadDuration(uploadSw.Elapsed.TotalMilliseconds, fileName);
                    _metrics?.IncrementClaimCheckUploads(fileName);
                }
                catch (Exception ex)
                {
                    uploadActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    uploadActivity?.SetTag("exception.type",    ex.GetType().FullName);
                    uploadActivity?.SetTag("exception.message", ex.Message);
                    throw;
                }

                var reference = NormalizeBlobReference(url) ?? url ?? string.Empty;
                uploadActivity?.SetTag("messaging.claimcheck.reference", reference);
                context.Tokens.Add(new TokenMessage
                {
                    Kind        = TokenKind.Message,
                    ContentType = "application/json",
                    Reference   = reference,
                    SizeBytes   = size
                });
                // Remove message payload to keep the in-flight context light.
                context.Message = default;
                context.SerializedPayload = null; // already uploaded; don't keep in-memory payload
                context.IsClaimCheckApplied = true;
            }
            else
            {
                // No claim-check applied: cache the serialized payload so callers can reuse it
                // and avoid serializing the context again before sending.
                context.SerializedPayload = serialized;
                context.IsClaimCheckApplied = false;
            }

            if (fileStream != null && !string.IsNullOrWhiteSpace(originalFileName))
            {
                string? url = await StorageProvider.UploadAsync(fileStream, originalFileName, cancellationToken);
                var reference = NormalizeBlobReference(url) ?? url ?? string.Empty;

                long sizeBytes;
                try { sizeBytes = fileStream.Length; }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "PrepareContextWithTokensAsync: unable to determine fileStream.Length; using -1 as fallback.");
                    sizeBytes = -1;
                }

                context.Tokens.Add(new TokenMessage
                {
                    Kind        = TokenKind.File,
                    ContentType = "application/octet-stream",
                    Reference   = reference,
                    SizeBytes   = sizeBytes
                });
            }
        }

        private static string? NormalizeBlobReference(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            raw = raw.Trim();

            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                var path = uri.AbsolutePath.TrimStart('/');
                var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    var container = segments[0];
                    var blobName = string.Join("/", segments.Skip(1));
                    return $"{container}/{blobName}";
                }
                return path;
            }
            return raw;
        }
    }
}
