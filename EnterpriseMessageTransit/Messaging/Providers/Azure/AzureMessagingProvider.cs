using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    [ExcludeFromCodeCoverage]
    internal class AzureMessagingProvider : IMessagingProvider
    {
        private readonly IMessagingAdapter _adapter;
        private readonly IMessageSerializer _serializer;
        private readonly ServiceBusClient _client;
        private readonly IMessageTransitConfigurationService _config;
        private readonly IEndpointResolver _endpointResolver;
        private readonly ILogger<AzureMessagingProvider> _logger;
        private readonly ServiceBusSenderCache _senderCache;
        private readonly AzureServiceBusProviderOptions _providerOptions;
        private readonly CircuitBreakerManager _circuitBreaker;
        private readonly IMetricsProvider? _metrics;

        public AzureMessagingProvider(
            IMessagingAdapter adapter,
            IMessageSerializer serializer,
            ServiceBusClient client,
            IMessageTransitConfigurationService config,
            IEndpointResolver endpointResolver,
            ILogger<AzureMessagingProvider> logger,
            ServiceBusSenderCache senderCache,
            AzureServiceBusProviderOptions providerOptions,
            CircuitBreakerManager circuitBreaker,
            IMetricsProvider? metrics = null)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _senderCache = senderCache ?? throw new ArgumentNullException(nameof(senderCache));
            _providerOptions = providerOptions ?? new AzureServiceBusProviderOptions();
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _metrics = metrics;
        }

        // A2 — SetInvocationMetadata délègue uniquement à l'adapter.
        // Plus d'état mutable _target/_consumer/_action dans le provider :
        // les valeurs voyagent dans MessagingOptions (Producer) ou via BindContext (Consumer).
        public void SetInvocationMetadata(string? target, string? consumer, string? action)
        {
            _adapter.SetInvocationMetadata(target, consumer, action);
        }

        public EndpointSettings Resolve(string? target)
        {
            // A2 — résolution directe depuis le target fourni, sans fallback sur un état d'instance.
            if (_endpointResolver.TryResolve(target, null, null, out var aud) && aud != null)
            {
                return aud;
            }
            throw new InvalidOperationException($"EndpointSettings non résolue (target='{target}').");
        }

        // A3 — BindContext délègue directement à l'adapter.
        // Le casting vers ServiceBusMessageActions (Microsoft.Azure.Functions.Worker) est encapsulé
        // dans AzureFunctionMessagingAdapter — ce provider générique n'en dépend plus.
        public void BindContext(object message, object actions)
        {
            _adapter.BindContext(message, actions);
        }

        public void BindContext(IMessageTransit message, object actions)
        {
            _adapter.BindContext(message, actions);
        }

        /// <summary>
        /// Délègue la lecture du <c>traceparent</c> W3C à l'adapter sous-jacent.
        /// Retourne <c>null</c> si le message ne contient pas de contexte de trace propagé.
        /// </summary>
        public string? GetTraceparent() => _adapter.GetTraceparent();

        private static bool IsFatalSendException(Exception ex)
        {
            if (ex is ObjectDisposedException) return true;
            if (ex is ServiceBusException sbEx && !sbEx.IsTransient) return true;
            return false;
        }

        /// <summary>
        /// Vérifie que le corps du message ne dépasse pas la limite applicative configurée via
        /// <see cref="TransportSettings.MaxMessageSizeKb"/>. Cette limite est indépendante de la
        /// limite Service Bus — elle peut refléter des contraintes réseau, métier ou opérationnelles.
        /// Sans configuration (0), aucune vérification applicative n'est faite ici.
        /// </summary>
        private static void EnforceMaxMessageSize(BinaryData body, long configuredMaxBytes, string messageId, string entityName)
        {
            if (configuredMaxBytes <= 0) return;
            var sizeBytes = body.ToMemory().Length;
            if (sizeBytes > configuredMaxBytes)
            {
                throw new ArgumentException(
                    $"Le message '{messageId}' ({sizeBytes / 1024} Ko) dépasse la limite applicative configurée " +
                    $"(MaxMessageSizeKb = {configuredMaxBytes / 1024} Ko) pour l'entité '{entityName}'. " +
                    $"Réduire la taille du payload ou réviser la configuration MaxMessageSizeKb.");
            }
        }

        public async Task SendAsync<T>(MessageTransitContext<T> context, MessagingOptions options, CancellationToken cancellationToken = default) where T : class
        {
            var audience = Resolve(options.Target);
            var retryPolicy = audience.Endpoint.SendRetry ?? ProducerSendRetryPolicy.Default;

            var payload = context.SerializedPayload ?? _serializer.Serialize(context);

            // Note : la limite applicative MaxMessageSizeKb est gérée en amont dans PublishAsync via
            // RequiresClaimCheck / PrepareClaimCheckAsync. Si le payload dépasse le seuil, il est déjà
            // externalisé en Blob Storage avant d'arriver ici — on ne re-vérifie pas la taille à ce niveau.

            var message = new ServiceBusMessage(payload)
            {
                MessageId     = context.MessageId ?? Guid.NewGuid().ToString(),
                CorrelationId = context.CorrelationId ?? context.MessageId ?? string.Empty,
                SessionId     = options.EnableSession ? context.SessionId ?? context.MessageId : null
            };

            if (options.Properties != null)
            {
                foreach (var kvp in options.Properties)
                {
                    message.ApplicationProperties[kvp.Key] = kvp.Value ?? string.Empty;
                }
            }

            using var activity = MessagingActivitySource.Source.StartActivity(
                "messaging.send",
                ActivityKind.Producer);
            activity?.SetTag("messaging.system",      "servicebus");
            activity?.SetTag("messaging.destination", audience.Endpoint.EntityName);
            activity?.SetTag("messaging.message_id",  context.MessageId);
            activity?.SetTag("messaging.session_id",  context.SessionId);

            // P4-T2 — Propagation W3C Trace Context dans les ApplicationProperties Service Bus.
            // "traceparent"   : lu par RoutingSlipExecutor/BaseConsumer pour créer le span messaging.consume.
            // "Diagnostic-Id" : lu par le trigger Azure Functions (host) pour parenter l'invocation Activity
            //                   avec le TraceId du producteur → end-to-end correlation dans Application Insights.
            if (Activity.Current?.Id is { } traceId)
            {
                message.ApplicationProperties["traceparent"]  = traceId;
                message.ApplicationProperties["Diagnostic-Id"] = traceId;
                var traceState = Activity.Current.TraceStateString;
                if (!string.IsNullOrEmpty(traceState))
                    message.ApplicationProperties["tracestate"] = traceState;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await SendSingleWithRetryAsync(
                    _senderCache.GetOrCreate(_client, audience.Endpoint.EntityName),
                    message,
                    audience.Endpoint.EntityName,
                    retryPolicy,
                    cancellationToken);
                sw.Stop();
                activity?.SetStatus(ActivityStatusCode.Ok);
                _metrics?.IncrementMessagesSent(audience.Endpoint.EntityName, audience.Endpoint.EntityType.ToString());
                _metrics?.RecordSendDuration(sw.Elapsed.TotalMilliseconds, audience.Endpoint.EntityName);
                _metrics?.SetCachedSenders(_senderCache.Count);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("exception.type",    ex.GetType().FullName);
                activity?.SetTag("exception.message", ex.Message);
                throw;
            }
        }

        public async Task SendBatchAsync<T>(IEnumerable<MessageTransitContext<T>> contexts, MessagingOptions options, CancellationToken cancellationToken = default) where T : class
        {
            var audience = Resolve(options.Target);
            var entityName = audience.Endpoint.EntityName;
            var retryPolicy = audience.Endpoint.SendRetry ?? ProducerSendRetryPolicy.Default;
            var sender = _senderCache.GetOrCreate(_client, entityName);

            // Construction des messages Service Bus (sérialisation + propagation trace).
            var messages = new List<ServiceBusMessage>();
            foreach (var ctx in contexts)
            {
                var payload = ctx.SerializedPayload ?? _serializer.Serialize(ctx);
                var msg = new ServiceBusMessage(payload)
                {
                    MessageId = ctx.MessageId ?? Guid.NewGuid().ToString(),
                    SessionId = ctx.SessionId
                };
                if (options.Properties != null)
                {
                    foreach (var kvp in options.Properties)
                        msg.ApplicationProperties[kvp.Key] = kvp.Value ?? string.Empty;
                }

                // P4-T2 — Propagation W3C Trace Context (depuis le span parent messaging.publish).
                if (Activity.Current?.Id is { } batchTraceId)
                {
                    msg.ApplicationProperties["traceparent"] = batchTraceId;
                    var batchTraceState = Activity.Current.TraceStateString;
                    if (!string.IsNullOrEmpty(batchTraceState))
                        msg.ApplicationProperties["tracestate"] = batchTraceState;
                }

                messages.Add(msg);
            }

            // Atomicité — Fail-fast avant tout envoi.
            // Un ServiceBusMessageBatch est atomique : soit tous les messages passent, soit aucun.
            using var batch = await sender.CreateMessageBatchAsync(cancellationToken);
            var maxSizeBytes = batch.MaxSizeInBytes;

            // Étape 1 — Limite applicative configurée par l'opérateur (MaxMessageSizeKb).
            // Indépendante de la limite Service Bus : peut refléter des contraintes réseau, métier
            // ou opérationnelles propres à l'organisation. Fail-fast avant tout appel au broker.
            if (audience.Endpoint.MaxMessageSizeKb > 0)
            {
                var configuredMaxBytes = (long)audience.Endpoint.MaxMessageSizeKb * 1024;
                foreach (var msg in messages)
                    EnforceMaxMessageSize(msg.Body, configuredMaxBytes, msg.MessageId, entityName);
            }

            // Étape 2 — Validation collective via TryAddMessage (source de vérité du broker).
            // Détecte le cas où les messages sont individuellement valides mais dépassent
            // collectivement la capacité du batch (overhead headers + propriétés applicatives).
            var collectivelyOversized = new List<string>();
            foreach (var msg in messages)
            {
                if (!batch.TryAddMessage(msg))
                    collectivelyOversized.Add(msg.MessageId);
            }

            if (collectivelyOversized.Count > 0)
            {
                throw new ArgumentException(
                    $"PublishBatchAsync: {collectivelyOversized.Count}/{messages.Count} message(s) ne rentrent pas " +
                    $"dans le batch atomique ({maxSizeBytes / 1024} Ko total, overhead headers inclus). Aucun message envoyé. " +
                    $"Diviser la collection en plusieurs appels PublishBatchAsync. " +
                    $"MessageIds en erreur : {string.Join(", ", collectivelyOversized)}");
            }

            // Envoi atomique — un seul batch Service Bus.
            using var batchActivity = MessagingActivitySource.Source.StartActivity(
                "messaging.send.batch",
                ActivityKind.Producer);
            batchActivity?.SetTag("messaging.system",              "servicebus");
            batchActivity?.SetTag("messaging.destination",         entityName);
            batchActivity?.SetTag("messaging.batch.message_count", batch.Count);
            var batchSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await SendBatchWithRetryAsync(sender, batch, entityName, retryPolicy, cancellationToken);
                batchSw.Stop();
                batchActivity?.SetStatus(ActivityStatusCode.Ok);
                _metrics?.IncrementMessagesSent(entityName, audience.Endpoint.EntityType.ToString());
                _metrics?.RecordSendDuration(batchSw.Elapsed.TotalMilliseconds, entityName);
                _metrics?.SetCachedSenders(_senderCache.Count);
            }
            catch (Exception ex)
            {
                batchActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                batchActivity?.SetTag("exception.type",    ex.GetType().FullName);
                batchActivity?.SetTag("exception.message", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// A4 — Envoi d'un message unique avec retry selon <see cref="ProducerSendRetryPolicy"/>.
        /// En cas d'erreur fatale, remplace le sender et réessaie jusqu'à <see cref="ProducerSendRetryPolicy.MaxAttempts"/>.
        /// </summary>
        private async Task SendSingleWithRetryAsync(
            ServiceBusSender sender,
            ServiceBusMessage message,
            string entityName,
            ProducerSendRetryPolicy retryPolicy,
            CancellationToken cancellationToken)
        {
            _circuitBreaker.EnsureCircuitAllows(entityName);

            var attempt = 0;
            while (true)
            {
                try
                {
                    await sender.SendMessageAsync(message, cancellationToken);
                    _circuitBreaker.RecordSuccess(entityName);
                    return;
                }
                catch (Exception ex) when (IsFatalSendException(ex) && attempt < retryPolicy.MaxAttempts)
                {
                    attempt++;
                    _circuitBreaker.RecordFailure(entityName);
                    _logger.LogWarning(ex,
                        "Envoi échoué (tentative {Attempt}/{Max}) pour l'entité {Entity}. Remplacement du sender...",
                        attempt, retryPolicy.MaxAttempts, entityName);
                    await Task.Delay(retryPolicy.InitialDelay * attempt, cancellationToken);
                    sender = _senderCache.ReplaceSender(_client, entityName);
                }
                catch (Exception ex)
                {
                    _circuitBreaker.RecordFailure(entityName);
                    // Tronquer le message d'exception pour éviter de polluer les logs avec des messages énormes
                    var truncatedMessage = TruncateForLog(ex.Message);
                    _logger.LogError(ex, "Échec définitif de l'envoi pour l'entité {Entity}. Raison: {Reason}", entityName, truncatedMessage);
                    throw new MessageSendException($"Échec envoi target='{entityName}'", ex);
                }
            }
        }

        /// <summary>
        /// A4 — Envoi d'un batch avec retry selon <see cref="ProducerSendRetryPolicy"/>.
        /// </summary>
        private async Task SendBatchWithRetryAsync(
            ServiceBusSender sender,
            ServiceBusMessageBatch batch,
            string entityName,
            ProducerSendRetryPolicy retryPolicy,
            CancellationToken cancellationToken)
        {
            _circuitBreaker.EnsureCircuitAllows(entityName);

            var attempt = 0;
            while (true)
            {
                try
                {
                    await sender.SendMessagesAsync(batch, cancellationToken);
                    _circuitBreaker.RecordSuccess(entityName);
                    return;
                }
                catch (Exception ex) when (IsFatalSendException(ex) && attempt < retryPolicy.MaxAttempts)
                {
                    attempt++;
                    _circuitBreaker.RecordFailure(entityName);
                    _logger.LogWarning(ex,
                        "Envoi batch échoué (tentative {Attempt}/{Max}) pour l'entité {Entity}. Remplacement du sender...",
                        attempt, retryPolicy.MaxAttempts, entityName);
                    await Task.Delay(retryPolicy.InitialDelay * attempt, cancellationToken);
                    sender = _senderCache.ReplaceSender(_client, entityName);
                }
                catch (Exception ex)
                {
                    _circuitBreaker.RecordFailure(entityName);
                    // Tronquer le message d'exception pour éviter de polluer les logs avec des messages énormes
                    var truncatedMessage = TruncateForLog(ex.Message);
                    _logger.LogError(ex, "Échec définitif de l'envoi batch pour l'entité {Entity}. Raison: {Reason}", entityName, truncatedMessage);
                    throw new MessageSendException($"Échec envoi batch target='{entityName}'", ex);
                }
            }
        }

        /// <summary>
        /// Tronque un message d'erreur pour les logs (1024 caractères max).
        /// Utilisé uniquement pour éviter de polluer les logs Azure Functions Output Window.
        /// </summary>
        private static string TruncateForLog(string? message, int maxChars = 1024)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            if (message.Length <= maxChars)
                return message;

            return message[..maxChars] + "... [TRUNCATED]";
        }

        public Task CompleteMessageAsync(CancellationToken cancellationToken = default) =>
            _adapter.CompleteMessageAsync(cancellationToken);

        public Task ImmediateRetryAsync(ImmediateRetryException exception, CancellationToken cancellationToken = default)
        {
            _metrics?.IncrementImmediateRetry(_adapter.GetMessage()?.MessageId ?? "unknown");
            return _adapter.ImmediateRetryAsync(exception, cancellationToken);
        }

        public Task ExponentialRetryAsync(ExponentialRetryException exception, CancellationToken cancellationToken = default)
        {
            _metrics?.IncrementExponentialRetry(_adapter.GetMessage()?.MessageId ?? "unknown");
            return _adapter.ExponentialRetryAsync(exception, cancellationToken);
        }

        public Task DeadLetterMessageAsync(Exception exception, CancellationToken cancellationToken = default)
        {
            _metrics?.IncrementMessagesDLQ(
                _adapter.GetMessage()?.MessageId ?? "unknown",
                exception.GetType().Name);
            return _adapter.DeadLetterMessageAsync(exception, cancellationToken);
        }

        public DeserializationResult<MessageTransitContext<T>> DeserializeMessageSafe<T>() where T : class
        {
            var msg = _adapter.GetMessage();
            var result = _serializer.DeserializeSafe<MessageTransitContext<T>>(msg?.Content?.ToString());

            // Hydrate CorrelationId and Attempt from the Service Bus envelope.
            // Même logique que RetryPolicyHandler.HandleExponentialRetryAsync :
            //   - Session  : DeliveryCount (incrémenté par le broker sur chaque Abandon)
            //   - No-session : ReferralCount (ApplicationProperty mis à jour lors du re-schedule)
            //                  Fallback sur DeliveryCount si absent (ex: ImmediateRetry/Abandon).
            if (result.IsSuccess && result.Value != null && msg != null)
            {
                if (string.IsNullOrWhiteSpace(result.Value.CorrelationId))
                {
                    result.Value.CorrelationId = !string.IsNullOrWhiteSpace(msg.CorrelationId)
                        ? msg.CorrelationId
                        : msg.MessageId;
                }

                bool isSession = !string.IsNullOrWhiteSpace(msg.SessionId);
                if (isSession)
                {
                    result.Value.Attempt = msg.DeliveryCount;
                }
                else
                {
                    int referralCount = 0;
                    if (msg.ApplicationProperties.TryGetValue(AzureMessagingProperties.ReferralCount, out var rc))
                    {
                        if (rc is int i) referralCount = i;
                        else if (rc is string s && int.TryParse(s, out var p)) referralCount = p;
                    }
                    result.Value.Attempt = referralCount + 1;
                }
            }

            return result;
        }

        // Note: sender disposal is handled by the singleton ServiceBusSenderCache.
    }
}
