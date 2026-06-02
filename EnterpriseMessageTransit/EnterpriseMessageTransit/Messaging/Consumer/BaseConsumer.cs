using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer
{
    /// <summary>
    /// Base Consumer — centralise la désérialisation, le binding et les actions message
    /// (Complete, DeadLetter, Retry).
    /// </summary>
    /// <typeparam name="TMessage">Représente le contenu de l'événement</typeparam>
    public abstract class BaseConsumer<TMessage> : BaseMessageTransit<TMessage>, IMessageConsumer<TMessage> where TMessage : class
    {
        private readonly string?            _targetName;
        private readonly string?            _consumerName;
        private readonly string?            _actionName;
        private readonly IMessageReceiver   _receiver;
        private readonly IMessageSettler    _settler;
        private readonly IMessageDeserializer _deserializer;
        private readonly IMessagingEndpointResolver _resolver;
        private readonly IConsumerTelemetry _telemetry;
        // R13 — scope structuré (MessageId/CorrelationId/…) actif de la désérialisation au settlement.
        private IDisposable? _messageScope;

        protected BaseConsumer(
            IMessagingProvider messagingProvider,
            ILogger logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            string? targetName   = null,
            string? consumerName = null,
            string? actionName   = null,
            IMetricsProvider? metricsProvider = null)
            : base(logger, config, serializer, storageProvider)
        {
            var p       = messagingProvider ?? throw new ArgumentNullException(nameof(messagingProvider));
            _receiver   = p;
            _settler    = p;
            _deserializer = p;
            _resolver   = p;
            _targetName   = targetName;
            _consumerName = consumerName;
            _actionName   = actionName;
            _telemetry    = new AzureConsumerTelemetry(metricsProvider);
        }

        private void ResetInvocationMetadata() =>
            _receiver.SetInvocationMetadata(_targetName, _consumerName, _actionName);

        public abstract Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<TMessage> context,
            CancellationToken cancellationToken);

        #region Désérialisation
        /// <summary>
        /// Désérialise le message entrant et retourne un <see cref="DeserializationResult{T}"/> encapsulant
        /// le résultat ou la raison d'échec.
        /// <para>
        /// <strong>C'est le consumer applicatif qui décide du settlement</strong> en fonction de
        /// <see cref="DeserializationResult{T}.FailureReason"/> — EMT ne complete, n'abandonne,
        /// ni ne dead-letter le message automatiquement.
        /// </para>
        /// </summary>
        public async Task<DeserializationResult<MessageTransitContext<TAnyMessage>>> DeserializeMessageAsync<TAnyMessage>(CancellationToken cancellationToken = default) where TAnyMessage : class
        {
            ResetInvocationMetadata();
            var sw = Stopwatch.StartNew();

            using var scope = _telemetry.BeginReceive(_resolver.GetTraceparent());

            var result = _deserializer.DeserializeMessageSafe<TAnyMessage>();

            if (!result.IsSuccess)
            {
                scope.MarkFailure(result.FailureReason.ToString(), result.ErrorMessage);
                _telemetry.RecordDeserializationFailure(result.FailureReason.ToString());
                Logger.LogWarning(
                    "DeserializeMessageAsync: échec désérialisation ({FailureReason}) — le consumer doit décider du settlement. Détail : {ErrorMessage}",
                    result.FailureReason, result.ErrorMessage);
                return result;
            }

            var ctx        = result.Value!;
            var entityName = ResolveEntityName();
            scope.MarkSuccess(ctx, entityName, _consumerName, _actionName);
            sw.Stop();
            _telemetry.RecordReceived(entityName, sw.Elapsed.TotalMilliseconds);

            // R13 — BeginScope : injecte MessageId/SessionId/CorrelationId dans customDimensions
            // pour tous les logs émis entre DeserializeMessageAsync et le settlement (Complete/DLQ/Retry).
            // Le scope est disposé automatiquement dans CompleteMessageAsync / DeadLetterMessageAsync / Retry*.
            _messageScope?.Dispose();
            _messageScope = Logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"]     = ctx.MessageId,
                ["CorrelationId"] = ctx.CorrelationId,
                ["SessionId"]     = ctx.SessionId,
                ["Consumer"]      = _consumerName,
                ["Action"]        = _actionName
            });

            return result;
        }

        private string ResolveEntityName()
        {
            try
            {
                var endpoint = _resolver.Resolve(_targetName);
                return endpoint.Target ?? endpoint.Endpoint?.EntityName ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        public void BindContext(object message, object actions)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            ResetInvocationMetadata();
            if (message is IMessageTransit mt)
                _receiver.BindContext(mt, actions);
            else
                _receiver.BindContext(message, actions);
        }
        #endregion

        #region Actions message
        public Task CompleteMessageAsync<TCurrent>(
            MessageTransitContext<TCurrent>? ctx = null,
            CancellationToken ct = default) where TCurrent : class
        {
            ResetInvocationMetadata();
            DisposeMessageScope();
            return _settler.CompleteMessageAsync(ct);
        }

        public Task CompleteMessageAsync(CancellationToken ct = default) =>
            CompleteMessageAsync<object>(null, ct);

        public Task DeadLetterMessageAsync(Exception ex, CancellationToken ct = default)
        {
            ResetInvocationMetadata();
            DisposeMessageScope();
            return _settler.DeadLetterMessageAsync(ex, ct);
        }

        protected Task ImmediateRetryAsync(ImmediateRetryException ex, CancellationToken ct = default)
        {
            ResetInvocationMetadata();
            DisposeMessageScope();
            return _settler.ImmediateRetryAsync(ex, ct);
        }

        protected Task ExponentialRetryAsync(ExponentialRetryException ex, CancellationToken ct = default)
        {
            ResetInvocationMetadata();
            DisposeMessageScope();
            return _settler.ExponentialRetryAsync(ex, ct);
        }

        private void DisposeMessageScope()
        {
            _messageScope?.Dispose();
            _messageScope = null;
        }
        #endregion
    }
}
