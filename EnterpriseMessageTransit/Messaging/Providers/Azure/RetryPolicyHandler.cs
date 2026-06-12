using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    /// <summary>
    /// Implémentation de IRetryPolicyHandler.
    /// Encapsule la logique de retry exponentiel et immédiat, ainsi que le dead-lettering.
    /// Améliore SRP en séparant l'adapter (binding) de la stratégie de retry.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal class RetryPolicyHandler : IRetryPolicyHandler
    {
        // Azure Service Bus official limit for deadLetterErrorDescription
        private const int MaxDeadLetterDescriptionLength = 4096;

        private readonly IMessageTransitConfigurationService _config;
        private readonly IJournalProvider _journalProvider;
        private readonly IEndpointResolver _endpointResolver;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSenderCache _senderCache;
        private readonly ISystemClock _systemClock;
        private readonly ILogger<RetryPolicyHandler> _logger;
        private readonly IMessageSerializer _serializer;

        // Métadonnées d'invocation (injectées via le contexte ou depuis l'adapter)
        private string? _target;
        private string? _consumer;
        private string? _action;

        public RetryPolicyHandler(
            IMessageTransitConfigurationService config,
            IJournalProvider journalProvider,
            IEndpointResolver endpointResolver,
            ServiceBusClient serviceBusClient,
            ServiceBusSenderCache senderCache,
            ISystemClock systemClock,
            IMessageSerializer serializer,
            ILogger<RetryPolicyHandler> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _journalProvider = journalProvider ?? throw new ArgumentNullException(nameof(journalProvider));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
            _senderCache = senderCache ?? throw new ArgumentNullException(nameof(senderCache));
            _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Définir les métadonnées d'invocation (target, consumer, action).
        /// </summary>
        public void SetMetadata(string? target, string? consumer, string? action)
        {
            _target = target;
            _consumer = consumer;
            _action = action;
        }

        /// <summary>
        /// Truncates a string to the Azure Service Bus dead-letter error description limit (4096 bytes UTF-8).
        /// </summary>
        private static string TruncateForDeadLetter(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            if (System.Text.Encoding.UTF8.GetByteCount(message) <= MaxDeadLetterDescriptionLength)
                return message;

            var truncated = message;
            while (System.Text.Encoding.UTF8.GetByteCount(truncated) > MaxDeadLetterDescriptionLength && truncated.Length > 0)
                truncated = truncated[..^1];

            const string Indicator = " ...[TRUNCATED]";
            if (System.Text.Encoding.UTF8.GetByteCount(truncated + Indicator) <= MaxDeadLetterDescriptionLength)
                truncated += Indicator;

            return truncated;
        }

        /// <summary>
        /// Détecte si un JSON reçu est indenté (formatted) ou compact.
        /// Stratégie : vérifier si le JSON contient des caractères de nouvelle ligne et/ou indentation typiques.
        /// </summary>
        /// <remarks>
        /// Retourne `true` si le JSON semble indenté, `false` sinon.
        /// Cette détection permet de re-sérialiser le message lors du retry
        /// en respectant le format original du message reçu.
        /// </remarks>
        private static bool IsJsonIndented(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            // Vérifier la présence de newlines et indentation (espaces/tabs) après { [ ou avant } ]
            return json.Contains("\n") || json.Contains("\r") || 
                   (json.Contains("  ") && (json.Contains("{\n") || json.Contains("[\n")));
        }

        /// <summary>
        /// Sérialise un objet en respectant le format d'indentation du JSON original.
        /// Utilisé dans le retry exponentiel pour conserver le format du message reçu.
        /// </summary>
        /// <remarks>
        /// Détecte si le JSON original est indenté et applique les mêmes options de sérialisation
        /// lors de la re-sérialisation du message mis à jour.
        /// </remarks>
        private string SerializePreservingIndentation<T>(T obj, string? originalJson) where T : class
        {
            bool isIndented = IsJsonIndented(originalJson);
            var options = isIndented 
                ? new JsonSerializerOptions { WriteIndented = true }
                : new JsonSerializerOptions { WriteIndented = false };
            
            return JsonSerializer.Serialize(obj, options);
        }

        /// <summary>
        /// Écrit une entrée journal de façon sécurisée (Pattern A5) :
        /// une défaillance du journal ne doit jamais bloquer le chemin de retry ou de settlement.
        /// </summary>
        private async Task SafeWriteJournalAsync(JournalEntry entry, CancellationToken cancellationToken)
        {
            try
            {
                await _journalProvider.WriteRecordAsync(entry, cancellationToken);
            }
            catch (Exception jEx)
            {
                _logger.LogWarning(jEx,
                    "Journal non écrit après settlement (pattern A5 — settlement réussi). MessageId={MessageId}",
                    entry.MessageId);
            }
        }

        public async Task HandleImmediateRetryAsync(
            ServiceBusReceivedMessage message,
            IMessageSettlementActions actions,
            ImmediateRetryException exception,
            CancellationToken cancellationToken = default)
        {
            var retryPolicy = _config.AppSettings?.RetryPolicy;
            int maxDeliveryCount = retryPolicy?.MaxDeliveryCount ?? 10;
            int attempt = message.DeliveryCount;

            // R13 — BeginScope pour enrichir tous les logs de cette méthode
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"]     = message.MessageId,
                ["CorrelationId"] = message.CorrelationId,
                ["SessionId"]     = message.SessionId,
                ["DeliveryCount"] = attempt
            });

            // P4-T3 — Restaurer le TraceId de l'activateur pour que les logs de retry
            // (y compris le log DLQ) partagent le même operation_Id que la saga d'origine.
            var traceparent = message.ApplicationProperties.TryGetValue("traceparent", out var tp)
                ? tp?.ToString() : null;
            Activity.Current?.SetTag("messaging.source.traceparent", traceparent);

            using var activity = traceparent != null
                ? MessagingActivitySource.Source.StartActivity("messaging.retry.immediate", ActivityKind.Internal, parentId: traceparent)
                : MessagingActivitySource.Source.StartActivity("messaging.retry.immediate", ActivityKind.Internal);
            activity?.SetTag("messaging.system",         "servicebus");
            activity?.SetTag("messaging.message_id",     message.MessageId);
            activity?.SetTag("messaging.delivery_count", attempt);
            activity?.SetTag("messaging.max_delivery",   maxDeliveryCount);

            try
            {
                _logger.LogWarning(
                    "Retry immédiat : tentative {Attempt}/{Max}. MessageId={MessageId}",
                    attempt, maxDeliveryCount, message.MessageId);

                if (attempt < maxDeliveryCount)
                {
                    await actions.AbandonAsync(
                        new Dictionary<string, object> { [AzureMessagingProperties.ReferralCount] = attempt },
                        cancellationToken);

                    var entry = new JournalEntry(
                        _consumer ?? "RetryPolicyHandler",
                        _action ?? "ImmediateRetry",
                        message.MessageId,
                        message.CorrelationId,
                        _target ?? message.Subject,
                        OperationMode.RETRY,
                        exception.StatusCode ?? 0,
                        attempt,
                        maxDeliveryCount,
                        string.Empty,
                        message.EnqueuedTime.UtcDateTime,
                        null,
                        message.SessionId,
                        _config.AppSettings?.ApplicationName);

                    await SafeWriteJournalAsync(entry, cancellationToken);
                    activity?.SetTag("messaging.retry.outcome", "abandon");
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                else
                {
                    _logger.LogError(
                        "Retry immédiat : nombre maximal de tentatives atteint, envoi en file des lettres mortes. MessageId={MessageId} Tentative={DeliveryCount}",
                        message.MessageId, attempt);

                    await actions.DeadLetterAsync("MaxDeliveryCountExceeded", TruncateForDeadLetter(exception.Message), cancellationToken);

                    var entryDlq = new JournalEntry(
                        _consumer ?? "RetryPolicyHandler",
                        _action ?? "ImmediateRetry",
                        message.MessageId,
                        message.CorrelationId,
                        _target ?? message.Subject,
                        OperationMode.DLQ,
                        exception.StatusCode ?? 0,
                        attempt,
                        maxDeliveryCount,
                        exception.Message,
                        message.EnqueuedTime.UtcDateTime,
                        message.DeadLetterSource,
                        message.SessionId,
                        _config.AppSettings?.ApplicationName);

                    await SafeWriteJournalAsync(entryDlq, cancellationToken);
                    activity?.SetTag("messaging.retry.outcome", "dlq");
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Erreur inattendue lors du retry immédiat — message envoyé en file des lettres mortes. MessageId={MessageId} Tentative={DeliveryCount}",
                    message.MessageId, message.DeliveryCount);

                await actions.DeadLetterAsync("ImmediateRetryException", TruncateForDeadLetter(exception.Message), cancellationToken);

                var entryEx = new JournalEntry(
                    _consumer ?? "RetryPolicyHandler",
                    _action ?? "ImmediateRetryException",
                    message.MessageId,
                    message.CorrelationId,
                    _target ?? message.Subject,
                    OperationMode.DLQ,
                    (int)System.Net.HttpStatusCode.InternalServerError,
                    message.DeliveryCount,
                    maxDeliveryCount,
                    exception.Message,
                    message.EnqueuedTime.UtcDateTime,
                    null,
                    message.SessionId,
                    _config.AppSettings?.ApplicationName);

                await SafeWriteJournalAsync(entryEx, cancellationToken);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("exception.type",    ex.GetType().FullName);
                activity?.SetTag("exception.message", ex.Message);
                throw;
            }
        }

        public async Task HandleExponentialRetryAsync(
            ServiceBusReceivedMessage message,
            IMessageSettlementActions actions,
            ExponentialRetryException exception,
            CancellationToken cancellationToken = default)
        {
            var retryPolicy = _config.AppSettings?.RetryPolicy;
            int maxDeliveryCount = retryPolicy?.MaxDeliveryCount ?? 10;

            // R13 — BeginScope
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"]     = message.MessageId,
                ["CorrelationId"] = message.CorrelationId,
                ["SessionId"]     = message.SessionId
            });

            bool isSession = !string.IsNullOrWhiteSpace(message.SessionId);
            int attempt;

            if (isSession)
            {
                attempt = message.DeliveryCount;
            }
            else
            {
                int referralCount = 0;
                if (message.ApplicationProperties.TryGetValue("ReferralCount", out var rc))
                {
                    if (rc is int i)
                    {
                        referralCount = i;
                    }
                    else if (rc is string s && int.TryParse(s, out var p))
                    {
                        referralCount = p;
                    }
                }
                attempt = referralCount + 1;
            }

            // P4-T3 — Restaurer le TraceId de l'activateur.
            // HandleExponentialRetryAsync est appelé APRÈS que RoutingSlipExecutor a lancé
            // ExponentialRetryException, ce qui a disposé le consumeActivity (TraceId A).
            // Activity.Current est redevenu l'invocation Azure Functions (TraceId X).
            // On relit le traceparent depuis le message pour rétablir la corrélation.
            var traceparent = message.ApplicationProperties.TryGetValue("traceparent", out var tp)
                ? tp?.ToString() : null;
            Activity.Current?.SetTag("messaging.source.traceparent", traceparent);

            using var activity = traceparent != null
                ? MessagingActivitySource.Source.StartActivity("messaging.retry.exponential", ActivityKind.Internal, parentId: traceparent)
                : MessagingActivitySource.Source.StartActivity("messaging.retry.exponential", ActivityKind.Internal);
            activity?.SetTag("messaging.system",         "servicebus");
            activity?.SetTag("messaging.message_id",     message.MessageId);
            activity?.SetTag("messaging.session_id",     message.SessionId);
            activity?.SetTag("messaging.delivery_count", attempt);
            activity?.SetTag("messaging.max_delivery",   maxDeliveryCount);
            activity?.SetTag("messaging.retry.session",  isSession ? "true" : "false");

            // DLQ si max atteint
            if (attempt > maxDeliveryCount)
            {
                _logger.LogError(
                    "Retry exponentiel : nombre maximal de tentatives atteint, envoi en file des lettres mortes. MessageId={MessageId} Tentative={DeliveryCount} SessionId={SessionId}",
                    message.MessageId, attempt, message.SessionId);

                await actions.DeadLetterAsync("MaxDeliveryCountExceeded", TruncateForDeadLetter(exception.Message), cancellationToken);

                var entryDlqExp = new JournalEntry(
                    _consumer ?? "RetryPolicyHandler",
                    _action ?? "ExponentialRetry",
                    message.MessageId,
                    message.CorrelationId,
                    _target ?? message.Subject,
                    OperationMode.DLQ,
                    exception.StatusCode ?? 0,
                    attempt,
                    maxDeliveryCount,
                    exception.Message,
                    message.EnqueuedTime.UtcDateTime,
                    null,
                    message.SessionId,
                    _config.AppSettings?.ApplicationName);

                await SafeWriteJournalAsync(entryDlqExp, cancellationToken);
                activity?.SetTag("messaging.retry.outcome", "dlq");
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            // Calcul délai exponentiel
            double baseMs    = retryPolicy?.InitialDelay.TotalMilliseconds ?? 500;   // default 500ms
            double maxMs     = retryPolicy?.MaxDelay.TotalMilliseconds     ?? 60_000; // default 60s
            double computedMs = baseMs * Math.Pow(2, attempt - 1);

            _logger.LogDebug(
                "Retry exponentiel — calcul du délai : politique={Politique} BaseMs={BaseMs} MaxMs={MaxMs} DélaiCalculé={ComputedMs}ms Tentative={Attempt} MessageId={MessageId}",
                retryPolicy == null ? "défaut" : "configurée",
                baseMs, maxMs, computedMs, attempt, message.MessageId);

            if (retryPolicy?.UseJitter == true)
            {
                double jitter = Random.Shared.NextDouble();
                computedMs *= (0.85 + jitter * 0.3);
            }
            if (computedMs > maxMs)  computedMs = maxMs;
            if (computedMs < baseMs) computedMs = baseMs;

            var delay = TimeSpan.FromMilliseconds(computedMs);
            var scheduledTime = _systemClock.UtcNow.Add(delay);

            // === SCÉNARIO AVEC SESSION ===
            if (isSession)
            {
                _logger.LogWarning(
                    "ExponentialRetry (session): Attempt {Attempt}/{Max} MessageId={MessageId} SessionId={SessionId} — session verrouillée pendant {DelayMs}ms (ordre FIFO préservé)",
                    attempt, maxDeliveryCount, message.MessageId, message.SessionId, (long)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);

                var retryProperties = new Dictionary<string, object>
                {
                    ["ReferralCount"] = attempt
                };
                if (!string.IsNullOrWhiteSpace(_target))   retryProperties["Target"]   = _target!;
                if (!string.IsNullOrWhiteSpace(_consumer)) retryProperties["Consumer"] = _consumer!;
                if (!string.IsNullOrWhiteSpace(_action))   retryProperties["Action"]   = _action!;

                await actions.AbandonAsync(retryProperties, cancellationToken);

                var entrySession = new JournalEntry(
                    _consumer ?? "RetryPolicyHandler",
                    _action ?? "ExponentialRetry(Session)",
                    message.MessageId,
                    message.CorrelationId,
                    _target ?? message.Subject,
                    OperationMode.RETRY,
                    exception.StatusCode ?? 0,
                    attempt,
                    maxDeliveryCount,
                    string.Empty,
                    message.EnqueuedTime.UtcDateTime,
                    null,
                    message.SessionId,
                    _config.AppSettings?.ApplicationName);

                await SafeWriteJournalAsync(entrySession, cancellationToken);
                activity?.SetTag("messaging.retry.outcome", "abandon-session");
                activity?.SetTag("messaging.retry.delay_ms", (long)delay.TotalMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            // === SCÉNARIO SANS SESSION ===
            // ℹ️ Duplicate detection et retry exponentiel (no session)
            // -----------------------------------------------------------------------------------
            // Un nouveau MessageId est généré pour le retry schedulé afin d'éviter le rejet
            // silencieux par la duplicate detection d'Azure Service Bus.
            // Le MessageId original est préservé dans CorrelationId pour la traçabilité
            // de bout en bout : premier retry → message.MessageId, retries suivants → message.CorrelationId.
            // -----------------------------------------------------------------------------------
            _logger.LogWarning(
                "Retry exponentiel (sans session) : tentative {Attempt}/{Max}, republication planifiée à {ScheduledTime}. MessageId={MessageId}",
                attempt, maxDeliveryCount, scheduledTime, message.MessageId);

            if (!_endpointResolver.TryResolve(_target, _consumer, _action, out var audience) || audience == null)
            {
                throw new InvalidOperationException("Endpoint non résolu pour retry.");
            }

            var entityName = audience.Endpoint.EntityName;
            var sender = _senderCache.GetOrCreate(_serviceBusClient, entityName);

            var newMessageId = Guid.NewGuid().ToString("N");

            // Update the MessageId inside the serialized context body so the consumer
            // deserializes the correct MessageId after the retry is delivered.
            // Preserve the original indentation format (compact vs indented) to maintain consistency.
            var originalBody = message.Body.ToString();
            var updatedBody = originalBody;
            var contextResult = _serializer.DeserializeSafe<MessageTransitContext<object>>(originalBody);
            if (contextResult.IsSuccess && contextResult.Value != null)
            {
                contextResult.Value.MessageId = newMessageId;
                updatedBody = SerializePreservingIndentation(contextResult.Value, originalBody);
            }

            var retryMessage = new ServiceBusMessage(new BinaryData(System.Text.Encoding.UTF8.GetBytes(updatedBody)))
            {
                // New MessageId for the scheduled retry to avoid duplicate detection conflicts.
                // CorrelationId always carries the ORIGINAL MessageId for end-to-end traceability:
                // - First retry:      message.CorrelationId is null → use message.MessageId (the original)
                // - Subsequent retry: message.CorrelationId already holds the original → preserve it
                MessageId     = newMessageId,
                CorrelationId = !string.IsNullOrWhiteSpace(message.CorrelationId)
                                    ? message.CorrelationId   // already the original MessageId
                                    : message.MessageId,      // first retry: capture the original
                Subject       = message.Subject,
                ContentType   = message.ContentType,
                TimeToLive    = message.TimeToLive
            };
            foreach (var kv in message.ApplicationProperties)
            {
                retryMessage.ApplicationProperties[kv.Key] = kv.Value;
            }

            retryMessage.ApplicationProperties["ReferralCount"] = attempt;

            if (!string.IsNullOrWhiteSpace(_target))
            {
                retryMessage.ApplicationProperties["Target"] = _target!;
            }
            if (!string.IsNullOrWhiteSpace(_consumer))
            {
                retryMessage.ApplicationProperties["Consumer"] = _consumer!;
            }
            if (!string.IsNullOrWhiteSpace(_action))
            {
                retryMessage.ApplicationProperties["Action"] = _action!;
            }
            
            await sender.ScheduleMessageAsync(retryMessage, scheduledTime, cancellationToken);
            await actions.CompleteAsync(cancellationToken);
            var entryFinal = new JournalEntry(
                _consumer ?? "RetryPolicyHandler",
                _action ?? "ExponentialRetry",
                retryMessage.MessageId,
                retryMessage.CorrelationId,
                _target ?? retryMessage.Subject,
                OperationMode.RETRY,
                exception.StatusCode ?? 0,
                attempt,
                maxDeliveryCount,
                string.Empty,
                message.EnqueuedTime.UtcDateTime,
                null,
                message.SessionId,
                _config.AppSettings?.ApplicationName);

            await SafeWriteJournalAsync(entryFinal, cancellationToken);
            activity?.SetTag("messaging.retry.outcome",      "schedule");
            activity?.SetTag("messaging.retry.delay_ms",     (long)delay.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }
}
