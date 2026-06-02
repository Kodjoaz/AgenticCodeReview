using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure.Functions
{
    /// <summary>
    /// Adaptateur interne qui enveloppe <see cref="ServiceBusMessageActions"/> et
    /// <see cref="ServiceBusReceivedMessage"/> derrière <see cref="IMessageSettlementActions"/>.
    /// Isole les couches métier (RetryPolicyHandler, AzureFunctionMessagingAdapter) du SDK
    /// Azure Functions Worker — seul cet adaptateur connaît les types concrets Azure.
    /// </summary>
    /// <remarks>Phase 2 (P2-C5) — découplage Functions.</remarks>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal sealed class ServiceBusMessageActionsAdapter : IMessageSettlementActions
    {
        private readonly ServiceBusReceivedMessage _message;
        private readonly ServiceBusMessageActions _actions;

        public ServiceBusMessageActionsAdapter(
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions)
        {
            _message = message ?? throw new ArgumentNullException(nameof(message));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        /// <inheritdoc/>
        public Task CompleteAsync(CancellationToken cancellationToken = default)
            => _actions.CompleteMessageAsync(_message, cancellationToken);

        /// <inheritdoc/>
        public Task AbandonAsync(
            IDictionary<string, object>? propertiesToModify = null,
            CancellationToken cancellationToken = default)
            => _actions.AbandonMessageAsync(_message, propertiesToModify, cancellationToken);

        /// <inheritdoc/>
        public Task DeadLetterAsync(
            string reason,
            string? description = null,
            CancellationToken cancellationToken = default)
            => _actions.DeadLetterMessageAsync(_message, null, reason, description, cancellationToken);
    }
}
