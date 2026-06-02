using Microsoft.Extensions.DependencyInjection;
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
        private readonly IMessagingProvider _messagingProvider;
        private readonly IServiceScopeFactory _scopeFactory;

        protected BaseRoutingSlipFunction(
            IMessagingProvider messagingProvider,
            IServiceScopeFactory scopeFactory)
        {
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

            try
            {
                await executeAsync(executor, _messagingProvider, cancellationToken);
            }
            catch (ImmediateRetryException ex)
            {
                await _messagingProvider.ImmediateRetryAsync(ex, cancellationToken);
            }
            catch (ExponentialRetryException ex)
            {
                await _messagingProvider.ExponentialRetryAsync(ex, cancellationToken);
            }
        }
    }
}