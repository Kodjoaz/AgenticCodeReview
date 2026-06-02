using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Base pour les Azure Functions/Workers qui exécutent une étape de routing slip.
    ///
    /// Centralise la résolution scoped de <see cref="IRoutingSlipExecutor"/>,
    /// le binding du message entrant et les métadonnées d'invocation afin que
    /// le code applicatif reste limité au trigger et au logging.
    /// </summary>
    public abstract class BaseRoutingSlipFunction
    {
        private readonly ILogger _logger;
        private readonly IMessagingProvider _messagingProvider;
        private readonly IServiceScopeFactory _scopeFactory;

        protected BaseRoutingSlipFunction(
            ILogger logger,
            IMessagingProvider messagingProvider,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messagingProvider = messagingProvider ?? throw new ArgumentNullException(nameof(messagingProvider));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        /// <summary>
        /// Exécute une étape de routing slip déclenchée par une queue.
        /// </summary>
        protected Task ProcessStepAsync<TArgs>(
            string functionName,
            object message,
            object actions,
            CancellationToken cancellationToken)
            where TArgs : class
            => RunStepAsync<TArgs>(
                functionName,
                message,
                actions,
                static (executor, provider, ct) => executor.ProcessAsync(provider, ct),
                cancellationToken);

        /// <summary>
        /// Exécute une étape de routing slip déclenchée par un topic/subscription.
        /// </summary>
        protected Task ExecuteStepAsync<TArgs>(
            string functionName,
            object message,
            object actions,
            CancellationToken cancellationToken)
            where TArgs : class
            => RunStepAsync<TArgs>(
                functionName,
                message,
                actions,
                static (executor, provider, ct) => executor.ExecuteAsync(provider, ct),
                cancellationToken);

        private async Task RunStepAsync<TArgs>(
            string functionName,
            object message,
            object actions,
            Func<IRoutingSlipExecutor, IMessagingProvider, CancellationToken, Task> executeAsync,
            CancellationToken cancellationToken)
            where TArgs : class
        {
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("functionName est requis.", nameof(functionName));

            using var scope = _scopeFactory.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredKeyedService<IRoutingSlipExecutor>(typeof(TArgs));

            _messagingProvider.SetInvocationMetadata(functionName, null, null);
            _messagingProvider.BindContext(message, actions);

            var sbMessage = message as ServiceBusReceivedMessage;

            // R13 — BeginScope : injecte MessageId/CorrelationId/SessionId/DeliveryCount dans
            // customDimensions pour tous les logs émis pendant le traitement de cette étape
            // (executor, activité, retry, DLQ). Identique à la stratégie BaseConsumer.
            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"]     = sbMessage?.MessageId,
                ["CorrelationId"] = sbMessage?.CorrelationId,
                ["SessionId"]     = sbMessage?.SessionId,
                ["DeliveryCount"] = sbMessage?.DeliveryCount ?? 0,
                ["Function"]      = functionName
            });

            try
            {
                await executeAsync(executor, _messagingProvider, cancellationToken);
            }
            catch (ImmediateRetryException ex)
            {
                _logger.LogWarning(
                    "BaseRoutingSlipFunction: ImmediateRetry livraison={DeliveryCount} — {Reason}",
                    sbMessage?.DeliveryCount ?? 0, ex.Message);
                await _messagingProvider.ImmediateRetryAsync(ex, cancellationToken);
            }
            catch (ExponentialRetryException ex)
            {
                _logger.LogWarning(
                    "BaseRoutingSlipFunction: ExponentialRetry livraison={DeliveryCount} — {Reason}",
                    sbMessage?.DeliveryCount ?? 0, ex.Message);
                await _messagingProvider.ExponentialRetryAsync(ex, cancellationToken);
            }
        }
    }
}