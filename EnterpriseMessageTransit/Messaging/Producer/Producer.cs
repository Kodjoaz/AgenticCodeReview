using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Producer multi Target permettant de publier des messages de différents types spécifiques dans différentes entités.
    /// </summary>
    /// <typeparam name="TMessage">Représente le contenu de l'événement</typeparam>
    public class Producer<TMessage> : BaseMessageTransit<TMessage>, IMessageProducer<TMessage>, IProducerPatterns where TMessage : class
    {
        private readonly IMessagingProvider _messagingProvider;
        private readonly IJournalProvider _journal;
        private readonly ISystemClock _systemClock;
        private readonly IMessageTargetMap? _targetMap;

        public Producer(
            IMessagingProvider messagingProvider,
            IJournalProvider journal,
            ILogger<Producer<TMessage>> logger,
            IProducerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            ISystemClock systemClock,
            IMessageTargetMap? targetMap = null)
            : base(logger, config, serializer, storageProvider)
        {
            _messagingProvider = messagingProvider ?? throw new ArgumentNullException(nameof(messagingProvider));
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
            _targetMap = targetMap;
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
        /// Chaîne de résolution : PublishOptions.Target → IMessageTargetMap → Target (constructeur) → mono-audience.
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

        public virtual async Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(
            MessageTransitContext<TMessage> context,
            Dictionary<string, object>? properties = null,
            ClaimCheckOptions? claimCheckOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await PublishCoreAsync(context, properties, claimCheckOptions, cancellationToken);
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

            // Valeur par défaut si non fourni
            claimCheckOptions ??= ClaimCheckOptions.None;

            if (string.IsNullOrWhiteSpace(context.MessageId))
            {
                context.MessageId = Guid.NewGuid().ToString("N");
            }

            // Résolution automatique du target :
            // 1. Liaison DI via IMessageTargetMap (services.AddProducer<T>("target"))
            // 2. null = fallback mono-audience
            string? effectiveTarget = _targetMap?.ResolveTarget<TMessage>();

            EndpointSettings audience = _messagingProvider.Resolve(effectiveTarget);
            string? resolvedTarget = audience.Target;

            bool enableSession = audience.Endpoint.EnableSession;

            // Sessions activées au niveau du producer
            if (enableSession && string.IsNullOrWhiteSpace(context.SessionId))
            {
                throw new ArgumentNullException(
                    $"Le paramètre <{nameof(context.SessionId)}> est obligatoire lorsque {nameof(enableSession)} est à true.");
            }

            // Alignement traçabilité (CurrentStage)
            if (string.IsNullOrWhiteSpace(context.CurrentStage))
            {
                context.SetCurrentStage(resolvedTarget);
            }

            await PrepareClaimCheckAsync(
                context,
                claimCheckOptions.FileStream,
                claimCheckOptions.OriginalFileName,
                claimCheckOptions.ForceClaimCheck,
                cancellationToken);

            var options = new MessagingOptions
            {
                Properties = properties,
                EnableSession = enableSession,
                FileStream = claimCheckOptions.FileStream,
                OriginalFileName = claimCheckOptions.OriginalFileName,
                ForceClaimCheck = claimCheckOptions.ForceClaimCheck,
                Target = resolvedTarget
            };

            (string? consumer, string? action) = ExtractMessageProperties(properties);

            try
            {
                await _messagingProvider.SendAsync(context, options, cancellationToken);

                var journalEntry = JournalEntry.ForPublish(
                    consumer ?? "(none)",
                    action ?? "(none)",
                    context.MessageId ?? string.Empty,
                    context.SessionId ?? string.Empty,
                    resolvedTarget ?? string.Empty,
                    context.SessionId,
                    Config.AppSettings?.ApplicationName,
                    _systemClock.UtcNow.UtcDateTime);

                // A5 — Journal découplé du chemin critique : un échec de journalisation
                // ne doit pas faire échouer un envoi réussi sur Service Bus.
                try { await _journal.WriteRecordAsync(journalEntry, cancellationToken); }
                catch (Exception jEx) { Logger.LogWarning(jEx, "Journal failed (publish) — message sent but not journalized. MessageId={MessageId}", context.MessageId); }

                return MapToResponseContext(context, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MessageSendException($"Send failed for target='{resolvedTarget}': {ex.Message}", ex);
            }
        }

        public virtual async Task<IReadOnlyList<string>> PublishBatchAsync(
            IEnumerable<MessageTransitContext<TMessage>> contexts,
            Dictionary<string, object>? properties = null,
            ClaimCheckOptions? claimCheckOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            ValidateRoutingProperties(properties);
            cancellationToken.ThrowIfCancellationRequested();

            claimCheckOptions ??= ClaimCheckOptions.None;

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
                {
                    ctx.MessageId = Guid.NewGuid().ToString("N");
                }

                // Prepare claim-check / tokens per context
                await PrepareClaimCheckAsync(ctx, claimCheckOptions.FileStream, claimCheckOptions.OriginalFileName, claimCheckOptions.ForceClaimCheck, cancellationToken);
            }

            // Résolution du target et activation des sessions (identique à PublishCoreAsync)
            string? effectiveTarget = _targetMap?.ResolveTarget<TMessage>();
            EndpointSettings audience = _messagingProvider.Resolve(effectiveTarget);
            bool enableSession = audience.Endpoint.EnableSession;

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

            // Alignement CurrentStage pour la traçabilité
            foreach (var ctx in contextsList)
            {
                if (string.IsNullOrWhiteSpace(ctx.CurrentStage))
                {
                    ctx.SetCurrentStage(audience.Target);
                }
            }

            var options = new MessagingOptions
            {
                Properties = properties,
                EnableSession = enableSession,
                FileStream = claimCheckOptions.FileStream,
                OriginalFileName = claimCheckOptions.OriginalFileName,
                ForceClaimCheck = claimCheckOptions.ForceClaimCheck,
                Target = audience.Target
            };

            try
            {
                await _messagingProvider.SendBatchAsync(contextsList, options, cancellationToken);

                // C1 — extraction une seule fois avant la boucle (DRY)
                (string? consumer, string? action) = ExtractMessageProperties(properties);

                foreach (var ctx in contextsList)
                {
                    var journalEntry = JournalEntry.ForPublish(
                        consumer ?? "(none)",
                        action ?? "(none)",
                        ctx.MessageId ?? string.Empty,
                        ctx.SessionId ?? string.Empty,
                        ctx.CurrentStage ?? string.Empty,
                        ctx.SessionId,
                        Config.AppSettings?.ApplicationName,
                        _systemClock.UtcNow.UtcDateTime);

                    // A5 — Journal découplé du chemin critique
                    try { await _journal.WriteRecordAsync(journalEntry, cancellationToken); }
                    catch (Exception jEx) { Logger.LogWarning(jEx, "Journal failed (batch) — message sent but not journalized. MessageId={MessageId}", ctx.MessageId); }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MessageSendException($"Batch publish failed: {ex.Message}", ex);
            }

            // Returns MessageIds in the same order as the input collection.
            return contextsList.Select(ctx => ctx.MessageId!).ToList();
        }

        /// <summary>
        /// Request/Reply avec résolution automatique du target.
        /// Le target est résolu via IMessageTargetMap (DI) ou en fallback mono-audience.
        /// </summary>
        public virtual Task<MessageTransitContext<MessageTransitResponse>?> GetResponseAsync(
            MessageTransitContext<TMessage> context,
            RequestReplyOptions? replyOptions,
            CancellationToken cancellationToken = default)
        {
            return GetResponseCoreAsync(
                context,
                replyOptions?.Properties,
                replyOptions?.ClaimCheck?.FileStream,
                replyOptions?.ClaimCheck?.OriginalFileName,
                replyOptions?.ClaimCheck?.ForceClaimCheck ?? false,
                replyOptions?.EnableOffline ?? false,
                cancellationToken);
        }

        private async Task<MessageTransitContext<MessageTransitResponse>?> GetResponseCoreAsync(
            MessageTransitContext<TMessage> context,
            Dictionary<string, object>? properties,
            Stream? fileStream,
            string? originalFileName,
            bool forceClaimcheck,
            bool enableOffline,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(context.MessageId))
            {
                context.MessageId = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(context.SessionId))
            {
                context.SessionId = context.MessageId;
            }

            // Résolution automatique du target :
            // 1. Liaison DI via IMessageTargetMap (services.AddProducer<T>("target"))
            // 2. null = fallback mono-audience
            string? effectiveTarget = _targetMap?.ResolveTarget<TMessage>();

            EndpointSettings audience = _messagingProvider.Resolve(effectiveTarget);

            // CurrentStage pour la trace
            if (string.IsNullOrWhiteSpace(context.CurrentStage))
            {
                context.SetCurrentStage(audience.Target);
            }

            var options = new MessagingOptions
            {
                Properties = properties,
                FileStream = fileStream,
                OriginalFileName = originalFileName,
                ForceClaimCheck = forceClaimcheck,
                Target = audience.Target,
                EnableOffline = enableOffline
            };

            (string? consumer, string? action) = ExtractMessageProperties(properties);

            // P7 — appel direct sans indirection ExecuteRequestReplyAsync (aligné sur PublishCoreAsync)
            var rawResult = await _messagingProvider.RequestReplyAsync(context, options, cancellationToken);
            MessageTransitContext<MessageTransitResponse>? result = rawResult == null
                ? null
                : context.CopyWithResponse(rawResult.Message as MessageTransitResponse);

            if (result != null)
            {
                var journalEntry = JournalEntry.ForRequestReply(
                    consumer ?? "(none)",
                    action ?? "(none)",
                    context.MessageId ?? string.Empty,
                    context.SessionId ?? string.Empty,
                    audience.Target ?? string.Empty,
                    result.Message is MessageTransitResponse resp ? resp.StatusCode : 0,
                    context.SessionId,
                    Config.AppSettings?.ApplicationName,
                    _systemClock.UtcNow.UtcDateTime);

                // A5 — Journal découplé du chemin critique
                try { await _journal.WriteRecordAsync(journalEntry, cancellationToken); }
                catch (Exception jEx) { Logger.LogWarning(jEx, "Journal failed (request-reply) — response received but not journalized. MessageId={MessageId}", context.MessageId); }

                return result;
            }
            return null;
        }

        protected MessageTransitContext<MessageTransitResponse> MapToResponseContext<TAnyMessage>(
            MessageTransitContext<TAnyMessage> source,
            MessageTransitResponse? response)
            where TAnyMessage : class
        {
            // P4 — délègue à CopyWithResponse pour garantir qu'aucune propriété
            // future de MessageTransitContext ne soit oubliée dans ce mapping.
            return source.CopyWithResponse(response);
        }

        public string GetTargetEntityName(string? target = null)
        {
            string? effectiveTarget = target
                ?? _targetMap?.ResolveTarget<TMessage>();
            var aud = _messagingProvider.Resolve(effectiveTarget);
            if (aud.Endpoint?.EntityName == null)
            {
                throw new InvalidOperationException("EntityName not resolved.");
            }

            return aud.Endpoint.EntityName;
        }

        private static void ValidateRoutingProperties(Dictionary<string, object>? properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return;
            }

            // P5 — noms de clés via MessagePropertyKeys (plus de magic strings)
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
                if (properties.TryGetValue(MessagePropertyKeys.Consumer, out var consumerObj) && consumerObj is string consumerStr)
                {
                    consumer = consumerStr;
                }

                if (properties.TryGetValue(MessagePropertyKeys.Action, out var actionObj) && actionObj is string actionStr)
                {
                    action = actionStr;
                }
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
                string? url = await StorageProvider.UploadAsync(serialized, fileName, cancellationToken);
                var reference = NormalizeBlobReference(url) ?? url ?? string.Empty;
                context.Tokens.Add(new TokenMessage
                {
                    Kind = TokenKind.Message,
                    ContentType = "application/json",
                    Reference = reference,
                    SizeBytes = size
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
                try
                {
                    // certains streams (non seekable) peuvent throw NotSupportedException
                    sizeBytes = fileStream.Length;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "PrepareContextWithTokensAsync: unable to determine fileStream.Length; using -1 as fallback.");
                    sizeBytes = -1;
                }

                context.Tokens.Add(new TokenMessage
                {
                    Kind = TokenKind.File,
                    ContentType = "application/octet-stream",
                    Reference = reference,
                    SizeBytes = sizeBytes
                });
            }
        }

        private static string? NormalizeBlobReference(string? raw)
        {
            // Return a relative container/blob path and strip any query (SAS) or host information
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            raw = raw.Trim();

            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                // Use the absolute path to build a container/blob reference and ignore host/query
                var path = uri.AbsolutePath.TrimStart('/');
                var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    var container = segments[0];
                    var blobName = string.Join("/", segments.Skip(1));
                    return $"{container}/{blobName}";
                }

                // If path doesn't include container + blob, return the path without leading slash
                return path;
            }

            // If not an absolute URI, assume it's already a relative reference and return as-is
            return raw;
        }
    }
}
