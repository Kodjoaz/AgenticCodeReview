using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure.Functions;
using System.Diagnostics.CodeAnalysis;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure.Functions
{
    [ExcludeFromCodeCoverage]
    internal class AzureFunctionMessagingAdapter : IMessagingAdapter
    {
        private ServiceBusReceivedMessage? _message = null;
        private IMessageSettlementActions? _settlementActions = null;
        private readonly IMessageTransitConfigurationService _config;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSenderCache _senderCache;
        private readonly IRetryPolicyHandler _retryPolicyHandler;
        private readonly ILogger<AzureFunctionMessagingAdapter> _logger;
        private readonly IJournalProvider _journalProvider;
        private readonly IEndpointResolver _endpointResolver;
        private readonly ISystemClock _systemClock;

        // Métadonnées d'invocation (injectées par le provider)
        private string? _target;
        private string? _consumer;
        private string? _action;
        // Idempotence settlement : 0 = non finalisé, 1 = finalisé (Complete ou DeadLetter déjà appelé)
        private int _settled = 0;
        public ServiceBusReceivedMessage Message
        {
            get => _message ?? throw new InvalidOperationException($"{nameof(Message)} non initialisé — appelez BindContext avant d'utiliser l'adaptateur.");
            set => _message = value;
        }

        private IMessageSettlementActions SettlementActions
            => _settlementActions ?? throw new InvalidOperationException($"SettlementActions non initialisé — appelez BindContext avant d'utiliser l'adaptateur.");

        public AzureFunctionMessagingAdapter(
            IMessageTransitConfigurationService config,
            ServiceBusClient serviceBusClient,
            ServiceBusSenderCache senderCache,
            IRetryPolicyHandler retryPolicyHandler,
            IEndpointResolver endpointResolver,
            ISystemClock systemClock,
            ILogger<AzureFunctionMessagingAdapter> logger,
            IJournalProvider journalProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
            _senderCache = senderCache ?? throw new ArgumentNullException(nameof(senderCache));
            _retryPolicyHandler = retryPolicyHandler ?? throw new ArgumentNullException(nameof(retryPolicyHandler));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _journalProvider = journalProvider ?? throw new ArgumentNullException(nameof(journalProvider));
        }

        public void SetInvocationMetadata(string? target, string? consumer, string? action)
        {
            _target = target;
            _consumer = consumer;
            _action = action;
            _retryPolicyHandler.SetMetadata(target, consumer, action);
        }

        public void BindContext(object message, object actions)
        {
            ServiceBusReceivedMessage sbMessage = message as ServiceBusReceivedMessage
                ?? throw new ArgumentException("Types de message invalide pour l'adapter Extensions.");

            ServiceBusMessageActions sbActions = actions as ServiceBusMessageActions
                ?? throw new ArgumentException("Types d'actions invalides pour l'adapter Extensions.");

            Message = sbMessage;
            _settlementActions = new ServiceBusMessageActionsAdapter(sbMessage, sbActions);
            _settled = 0;
        }

        public void BindContext(IMessageTransit message, object actions)
        {
            if (message is IHasRawServiceBusMessage rawProvider)
            {
                var raw = rawProvider.RawMessage;
                ServiceBusMessageActions sbActions = actions as ServiceBusMessageActions
                    ?? throw new ArgumentException("Types d'actions invalides pour l'adapter Extensions.");

                Message = raw;
                _settlementActions = new ServiceBusMessageActionsAdapter(raw, sbActions);
                _settled = 0;
                return;
            }

            throw new ArgumentException("Typed BindContext only supports AzureFunctionMessageTransit in this adapter.");
        }

        public async Task CompleteMessageAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
            {
                _logger.LogDebug("CompleteMessageAsync: message déjà finalisé (idempotent), appel ignoré. MessageId={MessageId}", _message?.MessageId);
                return;
            }
            
            await SettlementActions.CompleteAsync(cancellationToken);

            // A5 — Journalisation découplée du chemin critique : un échec du journal
            // ne doit jamais faire échouer un settlement réussi.
            await SafeWriteJournalAsync(
                OperationMode.COMPLETE,
                _message?.DeliveryCount ?? 0,
                string.Empty,
                null,
                cancellationToken);
        }

        public async Task ImmediateRetryAsync(ImmediateRetryException exception, CancellationToken cancellationToken = default)
        {
            // Déléguer au service dédié pour améliorer SRP
            // (RetryPolicyHandler gère déjà sa propre journalisation via SafeWriteJournalAsync)
            await _retryPolicyHandler.HandleImmediateRetryAsync(Message, SettlementActions, exception, cancellationToken);
        }

        public async Task ExponentialRetryAsync(ExponentialRetryException exception, CancellationToken cancellationToken = default)
        {
            // Déléguer au service dédié pour améliorer SRP
            // (RetryPolicyHandler gère déjà sa propre journalisation via SafeWriteJournalAsync)
            await _retryPolicyHandler.HandleExponentialRetryAsync(Message, SettlementActions, exception, cancellationToken);
        }

        public async Task DeadLetterMessageAsync(Exception exception, CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
            {
                _logger.LogDebug("DeadLetterMessageAsync: message déjà finalisé (idempotent), appel ignoré. MessageId={MessageId}", _message?.MessageId);
                return;
            }

            // Log warning for ImmediateDLQException
            if (exception is ImmediateDLQException immediateDLQ)
            {
                _logger.LogWarning(
                    "Message immédiatement envoyé en Dead Letter Queue. MessageId={MessageId}, Reason={Reason}, StatusCode={StatusCode}",
                    _message?.MessageId,
                    immediateDLQ.Message,
                    immediateDLQ.StatusCode);
            }

            await SettlementActions.DeadLetterAsync(exception.GetType().Name, exception.Message, cancellationToken);

            // A5 — Journalisation découplée du chemin critique : un échec du journal
            // ne doit jamais faire échouer un DLQ réussi.
            await SafeWriteJournalAsync(
                OperationMode.DLQ,
                _message?.DeliveryCount ?? 0,
                exception.GetType().Name,
                _message?.DeadLetterSource,
                cancellationToken);
        }

        /// <summary>
        /// Écrit une entrée journal de façon sécurisée (Pattern A5) :
        /// une défaillance du journal ne doit jamais bloquer le settlement ou retry.
        /// </summary>
        private async Task SafeWriteJournalAsync(
            OperationMode mode,
            int deliveryCount,
            string reason,
            string? deadLetterSource,
            CancellationToken cancellationToken)
        {
            try
            {
                JournalEntry entry = mode switch
                {
                    OperationMode.COMPLETE => JournalEntry.ForPublish(
                        _consumer ?? "Consumer",
                        _action ?? "Unknown",
                        _message?.MessageId ?? string.Empty,
                        _message?.CorrelationId ?? string.Empty,
                        _target ?? _message?.Subject ?? string.Empty,
                        _message?.SessionId,
                        _config.AppSettings?.ApplicationName,
                        _message?.EnqueuedTime.UtcDateTime ?? _systemClock.UtcNow.UtcDateTime),

                    OperationMode.DLQ => JournalEntry.ForDLQ(
                        _consumer ?? "Consumer",
                        _action ?? "Unknown",
                        _message?.MessageId ?? string.Empty,
                        _message?.CorrelationId ?? string.Empty,
                        _target ?? _message?.Subject ?? string.Empty,
                        _message?.DeliveryCount ?? 0,
                        _config.AppSettings?.RetryPolicy?.MaxDeliveryCount ?? 10,
                        reason,
                        deadLetterSource,
                        _message?.SessionId,
                        _config.AppSettings?.ApplicationName,
                        _message?.EnqueuedTime.UtcDateTime ?? _systemClock.UtcNow.UtcDateTime),

                    _ => throw new InvalidOperationException($"Mode d'opération non supporté: {mode}")
                };

                await _journalProvider.WriteRecordAsync(entry, cancellationToken);
            }
            catch (Exception jEx)
            {
                _logger.LogWarning(jEx,
                    "Journal failed (consumer adapter) — settlement succeeded but not journalized. MessageId={MessageId}",
                    _message?.MessageId);
            }
        }

        public IMessageTransit GetMessage()
            => new AzureFunctionMessageTransit(Message);

        /// <summary>
        /// Lit le <c>traceparent</c> W3C depuis les <c>ApplicationProperties</c> du message
        /// Service Bus reçu. Retourne <c>null</c> si absent (message antérieur à P4-T2 ou
        /// producteur sans instrumentation).
        /// </summary>
        public string? GetTraceparent()
        {
            if (_message == null) return null;
            return _message.ApplicationProperties.TryGetValue("traceparent", out var tp)
                ? tp?.ToString()
                : null;
        }
    }
}
