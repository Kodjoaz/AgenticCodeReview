using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    /// <summary>
    /// Implémentation de IRetryPolicyHandler.
    /// Encapsule la logique de retry exponentiel et immédiat, ainsi que le dead-lettering.
    /// Améliore SRP en séparant l'adapter (binding) de la stratégie de retry.
    /// </summary>
    public class RetryPolicyHandler : IRetryPolicyHandler
    {
        private readonly IMessageTransitConfigurationService _config;
        private readonly IJournalProvider _journalProvider;
        private readonly IEndpointResolver _endpointResolver;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSenderCache _senderCache;
        private readonly ISystemClock _systemClock;
        private readonly ILogger<RetryPolicyHandler> _logger;

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
            ILogger<RetryPolicyHandler> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _journalProvider = journalProvider ?? throw new ArgumentNullException(nameof(journalProvider));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
            _senderCache = senderCache ?? throw new ArgumentNullException(nameof(senderCache));
            _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
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

        public async Task HandleImmediateRetryAsync(
            ServiceBusReceivedMessage message,
            object actions,
            ImmediateRetryException exception,
            CancellationToken cancellationToken = default)
        {
            // actions est de type ServiceBusMessageActions (du SDK Azure Functions Worker)
            dynamic sbActions = actions;
            var retryPolicy = _config.AppSettings?.RetryPolicy;
            int maxDeliveryCount = retryPolicy?.MaxDeliveryCount ?? 10;
            int attempt = message.DeliveryCount;

            try
            {
                _logger.LogWarning(
                    "ImmediateRetry: Attempt {Attempt}/{Max} MessageId={MessageId}",
                    attempt, maxDeliveryCount, message.MessageId);

                if (attempt < maxDeliveryCount)
                {
                    await sbActions.AbandonMessageAsync(message, null, cancellationToken);

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

                    await _journalProvider.WriteRecordAsync(entry, cancellationToken);
                }
                else
                {
                    _logger.LogError(
                        "ImmediateRetry: MaxDeliveryCount reached -> DLQ MessageId={MessageId} DeliveryCount={DeliveryCount}",
                        message.MessageId, attempt);

                    await sbActions.DeadLetterMessageAsync(message, null, "MaxDeliveryCountExceeded", exception.Message, cancellationToken);

                    var entry = new JournalEntry(
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

                    await _journalProvider.WriteRecordAsync(entry, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Erreur HandleImmediateRetryAsync -> DLQ forcé MessageId={MessageId} DeliveryCount={DeliveryCount}",
                    message.MessageId, message.DeliveryCount);

                await sbActions.DeadLetterMessageAsync(message, null, "ImmediateRetryException", exception.Message, cancellationToken);

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
                    ex.Message,
                    message.EnqueuedTime.UtcDateTime,
                    null,
                    message.SessionId,
                    _config.AppSettings?.ApplicationName);

                await _journalProvider.WriteRecordAsync(entryEx, cancellationToken);
                throw;
            }
        }

        public async Task HandleExponentialRetryAsync(
            ServiceBusReceivedMessage message,
            object actions,
            ExponentialRetryException exception,
            CancellationToken cancellationToken = default)
        {
            // actions est de type ServiceBusMessageActions (du SDK Azure Functions Worker)
            dynamic sbActions = actions;
            var retryPolicy = _config.AppSettings?.RetryPolicy;
            int maxDeliveryCount = retryPolicy?.MaxDeliveryCount ?? 10;

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

            // DLQ si max atteint
            if (attempt > maxDeliveryCount)
            {
                _logger.LogError(
                    "ExponentialRetry: MaxDeliveryCount reached -> DLQ MessageId={MessageId} DeliveryCount={DeliveryCount} SessionId={SessionId}",
                    message.MessageId, attempt, message.SessionId);

                await sbActions.DeadLetterMessageAsync(message, null, "MaxDeliveryCountExceeded", exception.Message, cancellationToken);

                var entry = new JournalEntry(
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

                await _journalProvider.WriteRecordAsync(entry, cancellationToken);
                return;
            }

            // Calcul délai exponentiel
            double baseMs = retryPolicy?.InitialDelay.TotalMilliseconds ?? (int)System.Net.HttpStatusCode.InternalServerError;
            double maxMs = retryPolicy?.MaxDelay.TotalMilliseconds ?? 60000;
            double computedMs = baseMs * Math.Pow(2, attempt - 1);
            if (retryPolicy?.UseJitter == true)
            {
                double jitter = Random.Shared.NextDouble();
                computedMs *= (0.85 + jitter * 0.3);
            }
            if (computedMs > maxMs)
            {
                computedMs = maxMs;
            }
            if (computedMs < baseMs)
            {
                computedMs = baseMs;
            }

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

                await sbActions.AbandonMessageAsync(message, retryProperties, cancellationToken);

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

                await _journalProvider.WriteRecordAsync(entrySession, cancellationToken);
                return;
            }

            // === SCÉNARIO SANS SESSION ===
            _logger.LogWarning(
                "ExponentialRetry (no session): Attempt {Attempt}/{Max} MessageId={MessageId}",
                attempt, maxDeliveryCount, message.MessageId);

            if (!_endpointResolver.TryResolve(_target, _consumer, _action, out var audience) || audience == null)
            {
                throw new InvalidOperationException("Endpoint non résolu pour retry.");
            }

            var entityName = audience.Endpoint.EntityName;
            var sender = _senderCache.GetOrCreate(_serviceBusClient, entityName);

            var retryMessage = new ServiceBusMessage(message.Body)
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Subject = message.Subject,
                ContentType = message.ContentType,
                CorrelationId = message.CorrelationId,
                TimeToLive = message.TimeToLive
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
            await sbActions.CompleteMessageAsync(message, cancellationToken);

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

            await _journalProvider.WriteRecordAsync(entryFinal, cancellationToken);
        }

        public async Task HandleDeadLetterAsync(
            ServiceBusReceivedMessage message,
            object actions,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            // actions est de type ServiceBusMessageActions (du SDK Azure Functions Worker)
            dynamic sbActions = actions;
            _logger.LogError(
                exception,
                "HandleDeadLetterAsync: sending message to DLQ MessageId={MessageId}",
                message.MessageId);

            await sbActions.DeadLetterMessageAsync(
                message,
                null,
                exception.GetType().Name,
                exception.Message,
                cancellationToken);

            var entry = new JournalEntry(
                _consumer ?? "RetryPolicyHandler",
                _action ?? "DeadLetter",
                message.MessageId,
                message.CorrelationId,
                _target ?? message.Subject,
                OperationMode.DLQ,
                (int)System.Net.HttpStatusCode.InternalServerError,
                message.DeliveryCount,
                _config.AppSettings?.RetryPolicy?.MaxDeliveryCount ?? 10,
                exception.Message,
                message.EnqueuedTime.UtcDateTime,
                null,
                message.SessionId,
                _config.AppSettings?.ApplicationName);

            await _journalProvider.WriteRecordAsync(entry, cancellationToken);
        }
    }
}
