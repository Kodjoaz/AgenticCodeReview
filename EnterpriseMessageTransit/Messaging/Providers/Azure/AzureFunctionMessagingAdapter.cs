using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    public class AzureFunctionMessagingAdapter : IMessagingAdapter
    {
        private ServiceBusReceivedMessage? _message = null;
        private ServiceBusMessageActions? _actions = null;
        private readonly IMessageTransitConfigurationService _config;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSenderCache _senderCache;
        private readonly IRetryPolicyHandler _retryPolicyHandler;
        private readonly ILogger<AzureFunctionMessagingAdapter> _logger;
        private readonly IJournalProvider _journalProvider;
        private readonly IEndpointResolver _endpointResolver;
        private readonly ISystemClock _systemClock;

        // Métadonnées d’invocation (injectées par le provider)
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
        public ServiceBusMessageActions Actions
        {
            get => _actions ?? throw new InvalidOperationException($"{nameof(Actions)} non initialisé — appelez BindContext avant d'utiliser l'adaptateur.");
            set => _actions = value;
        }

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
            // audience resolver injected
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
            Actions = sbActions;
            _settled = 0;
        }

        public void BindContext(IMessageTransit message, object actions)
        {
            if (message is AzureFunctionMessageTransit afmt)
            {
                var raw = afmt.RawMessage;
                ServiceBusMessageActions sbActions = actions as ServiceBusMessageActions
                    ?? throw new ArgumentException("Types d'actions invalides pour l'adapter Extensions.");

                Message = raw;
                Actions = sbActions;
                _settled = 0;
                return;
            }

            throw new ArgumentException("Typed BindContext only supports AzureFunctionMessageTransit in this adapter.");
        }

        public Task CompleteMessageAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
            {
                _logger.LogDebug("CompleteMessageAsync: message déjà finalisé (idempotent), appel ignoré. MessageId={MessageId}", _message?.MessageId);
                return Task.CompletedTask;
            }
            return Actions.CompleteMessageAsync(Message, cancellationToken);
        }

        public async Task ImmediateRetryAsync(ImmediateRetryException exception, CancellationToken cancellationToken = default)
        {
            // Déléguer au service dédié pour améliorer SRP
            await _retryPolicyHandler.HandleImmediateRetryAsync(Message, Actions, exception, cancellationToken);
        }

        public async Task ExponentialRetryAsync(ExponentialRetryException exception, CancellationToken cancellationToken = default)
        {
            // Déléguer au service dédié pour améliorer SRP
            await _retryPolicyHandler.HandleExponentialRetryAsync(Message, Actions, exception, cancellationToken);
        }

        public Task DeadLetterMessageAsync(Exception exception, CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
            {
                _logger.LogDebug("DeadLetterMessageAsync: message déjà finalisé (idempotent), appel ignoré. MessageId={MessageId}", _message?.MessageId);
                return Task.CompletedTask;
            }
            return Actions.DeadLetterMessageAsync(Message, null, exception.GetType().Name, exception.Message, cancellationToken);
        }

        public IMessageTransit GetMessage()
            => new AzureFunctionMessageTransit(Message);
    }
}
