using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
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
        private readonly IMetricsProvider? _metrics;

        /// <summary>
        /// Target logique de ce consumer (ex: "Car", "Hotel").
        /// Null = mono-audience (résolution automatique depuis le premier endpoint).
        /// Fourni via <c>AddConsumer&lt;T&gt;("target")</c> en multi-endpoint.
        /// </summary>
        private readonly string? _targetName;

        /// <summary>
        /// Nom logique du consumer (ex: "CarConsumer").
        /// Propagé à <see cref="IMessagingProvider.SetInvocationMetadata"/> pour le journal et le retry.
        /// </summary>
        private readonly string? _consumerName;

        /// <summary>
        /// Action logique du consumer (ex: "ReserverVoiture").
        /// Propagé à <see cref="IMessagingProvider.SetInvocationMetadata"/> pour le journal et le retry.
        /// </summary>
        private readonly string? _actionName;

        protected readonly IMessagingProvider MessagingProvider;

        protected BaseConsumer(
            IMessagingProvider messagingProvider,
            ILogger logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            string? targetName = null,
            string? consumerName = null,
            string? actionName = null,
            IMetricsProvider? metricsProvider = null)
            : base(logger, config, serializer, storageProvider)
        {
            MessagingProvider = messagingProvider ?? throw new ArgumentNullException(nameof(messagingProvider));
            _targetName   = targetName;
            _consumerName = consumerName;
            _actionName   = actionName;
            _metrics      = metricsProvider;
        }

        /// <summary>
        /// Restaure les métadonnées d'invocation du consumer (target, consumer, action).
        /// Appelé avant chaque opération pour s'assurer que le journal et le retry handler
        /// disposent du bon contexte, même après un appel précédent qui l'aurait modifié.
        /// </summary>
        protected void ResetInvocationMetadata() =>
            MessagingProvider.SetInvocationMetadata(_targetName, _consumerName, _actionName);

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
            var receiveSw = System.Diagnostics.Stopwatch.StartNew();

            // P4-T2 (consumer) — Rétablissement du contexte W3C propagé par le producteur.
            var traceparent = MessagingProvider.GetTraceparent();
            using var consumeActivity = MessagingActivitySource.Source.StartActivity(
                "messaging.consume",
                ActivityKind.Consumer,
                parentId: traceparent);
            consumeActivity?.SetTag("messaging.system", "servicebus");

            using var activity = MessagingActivitySource.Source.StartActivity(
                "messaging.deserialize",
                ActivityKind.Consumer);

            var result = MessagingProvider.DeserializeMessageSafe<TAnyMessage>();

            if (!result.IsSuccess)
            {
                activity?.SetTag("deserialization.failure_reason", result.FailureReason.ToString());
                activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage ?? result.FailureReason.ToString());
                consumeActivity?.SetStatus(ActivityStatusCode.Error, result.FailureReason.ToString());
                _metrics?.IncrementDeserializationFailure(result.FailureReason.ToString());
                Logger.LogWarning(
                    "DeserializeMessageAsync: échec désérialisation ({FailureReason}) — le consumer doit décider du settlement. Détail : {ErrorMessage}",
                    result.FailureReason, result.ErrorMessage);
                return result;
            }

            var ctx = result.Value!;
            var entityName = ResolveEntityName();

            consumeActivity?.SetTag("messaging.correlation_id", ctx.CorrelationId);
            consumeActivity?.SetTag("messaging.consumer",       _consumerName ?? GetType().Name);
            consumeActivity?.SetTag("messaging.action",         _actionName ?? string.Empty);
            consumeActivity?.SetTag("messaging.destination",    entityName);
            consumeActivity?.SetTag("messaging.status_code",    "200");
            consumeActivity?.SetTag("messaging.mode",           OperationMode.COMPLETE.ToString());

            activity?.SetTag("messaging.message_id",  ctx.MessageId);
            activity?.SetTag("messaging.session_id",  ctx.SessionId);
            activity?.SetTag("messaging.destination", entityName);
            activity?.SetTag("messaging.claimcheck",  ctx.IsClaimCheckApplied ? "true" : "false");

            activity?.SetStatus(ActivityStatusCode.Ok);
            consumeActivity?.SetStatus(ActivityStatusCode.Ok);
            receiveSw.Stop();
            _metrics?.IncrementMessagesReceived(entityName, "Consumer");
            _metrics?.RecordReceiveDuration(receiveSw.Elapsed.TotalMilliseconds, entityName);
            return result;
        }

        /// <summary>
        /// Résout le nom d'entité depuis la configuration locale du consumer.
        /// Symétrique à la logique Producer : utilise <c>_targetName</c> s'il est fourni (multi-endpoint),
        /// sinon fallback sur le premier endpoint (mono-audience).
        /// Retourne <c>"unknown"</c> uniquement si la résolution échoue (OTel/métriques — chemin non critique).
        /// </summary>
        private string ResolveEntityName()
        {
            try
            {
                var endpoint = MessagingProvider.Resolve(_targetName);
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
                MessagingProvider.BindContext(mt, actions);
            else
                MessagingProvider.BindContext(message, actions);
        }
        #endregion

        #region Actions message
        public Task CompleteMessageAsync<TCurrent>(
            MessageTransitContext<TCurrent>? ctx = null,
            CancellationToken ct = default) where TCurrent : class
        {
            ResetInvocationMetadata();
            return MessagingProvider.CompleteMessageAsync(ct);
        }

        public Task CompleteMessageAsync(CancellationToken ct = default) =>
            CompleteMessageAsync<object>(null, ct);

        public Task DeadLetterMessageAsync(Exception ex, CancellationToken ct = default)
        {
            ResetInvocationMetadata();
            return MessagingProvider.DeadLetterMessageAsync(ex, ct);
        }

        protected Task ImmediateRetryAsync(ImmediateRetryException ex, CancellationToken ct = default)
        {
            ResetInvocationMetadata();
            return MessagingProvider.ImmediateRetryAsync(ex, ct);
        }

        protected Task ExponentialRetryAsync(ExponentialRetryException ex, CancellationToken ct = default)
        {
            ResetInvocationMetadata();
            return MessagingProvider.ExponentialRetryAsync(ex, ct);
        }
        #endregion
    }
}
