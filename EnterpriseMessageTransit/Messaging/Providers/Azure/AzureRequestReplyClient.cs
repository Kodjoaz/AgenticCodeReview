using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    /// <summary>
    /// Implémentation Azure Service Bus du pattern Request/Reply.
    /// Utilise <see cref="ServiceBusSenderCache"/> pour l'envoi (pas de CreateSender ad-hoc)
    /// et désérialise la réponse comme <c>MessageTransitContext&lt;TResponse&gt;</c>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal sealed class AzureRequestReplyClient<TRequest, TResponse> : IRequestReplyClient<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusSenderCache _senderCache;
        private readonly IMessageSerializer _serializer;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IJournalProvider _journal;
        private readonly IMessageTransitConfigurationService _config;
        private readonly ISystemClock _systemClock;
        private readonly AzureServiceBusProviderOptions _providerOptions;
        private readonly ILogger<AzureRequestReplyClient<TRequest, TResponse>> _logger;
        private readonly IMessageTargetMap? _targetMap;
        private readonly IMetricsProvider? _metrics;

        public AzureRequestReplyClient(
            ServiceBusClient client,
            ServiceBusSenderCache senderCache,
            IMessageSerializer serializer,
            IEndpointResolver endpointResolver,
            IJournalProvider journal,
            IMessageTransitConfigurationService config,
            ISystemClock systemClock,
            ILogger<AzureRequestReplyClient<TRequest, TResponse>> logger,
            AzureServiceBusProviderOptions? providerOptions = null,
            IMessageTargetMap? targetMap = null,
            IMetricsProvider? metrics = null)
        {
            _client           = client           ?? throw new ArgumentNullException(nameof(client));
            _senderCache      = senderCache      ?? throw new ArgumentNullException(nameof(senderCache));
            _serializer       = serializer       ?? throw new ArgumentNullException(nameof(serializer));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _journal          = journal          ?? throw new ArgumentNullException(nameof(journal));
            _config           = config           ?? throw new ArgumentNullException(nameof(config));
            _systemClock      = systemClock      ?? throw new ArgumentNullException(nameof(systemClock));
            _logger           = logger           ?? throw new ArgumentNullException(nameof(logger));
            _providerOptions  = providerOptions  ?? new AzureServiceBusProviderOptions();
            _targetMap        = targetMap;
            _metrics          = metrics;
        }

        public async Task<MessageTransitContext<TResponse>?> GetResponseAsync(
            MessageTransitContext<TRequest> context,
            RequestReplyOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(context.MessageId))
                context.MessageId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(context.SessionId))
                context.SessionId = context.MessageId;
            if (string.IsNullOrWhiteSpace(context.CorrelationId))
                context.CorrelationId = context.MessageId;

            var properties    = options?.Properties;
            var enableOffline = options?.EnableOffline ?? false;

            // Résolution de l'endpoint de requête via TRequest
            string? requestTarget = _targetMap?.ResolveTarget<TRequest>();
            if (!_endpointResolver.TryResolve(requestTarget, null, null, out var requestAudience) || requestAudience == null)
                throw new InvalidOperationException(
                    $"Endpoint de requête non résolu (target='{requestTarget}'). " +
                    "Vérifier que AddRequestReplyClient<TRequest, TResponse>(requestTarget, replyTarget) est configuré.");

            // Résolution de l'endpoint de réponse via TResponse (queue distincte de la requête)
            string? replyTarget = _targetMap?.ResolveTarget<TResponse>();
            if (!_endpointResolver.TryResolve(replyTarget, null, null, out var replyAudience) || replyAudience == null)
                throw new InvalidOperationException(
                    $"Endpoint de réponse non résolu (target='{replyTarget}'). " +
                    "Utiliser AddRequestReplyClient<TRequest, TResponse>(requestTarget, replyTarget) avec les deux cibles.");

            // Sérialise le contexte complet (pas seulement context.Message) pour préserver les métadonnées
            var payload = _serializer.Serialize(context);
            var request = new ServiceBusMessage(payload)
            {
                MessageId        = context.MessageId,
                SessionId        = context.SessionId,
                ReplyToSessionId = context.SessionId
            };

            if (properties != null)
                foreach (var kvp in properties)
                    request.ApplicationProperties[kvp.Key] = kvp.Value ?? "";

            // Propagation W3C traceparent
            if (Activity.Current?.Id is { } traceId)
            {
                request.ApplicationProperties["traceparent"] = traceId;
                var traceState = Activity.Current.TraceStateString;
                if (!string.IsNullOrEmpty(traceState))
                    request.ApplicationProperties["tracestate"] = traceState;
            }

            using var activity = MessagingActivitySource.Source.StartActivity(
                "messaging.request_reply", ActivityKind.Producer);
            activity?.SetTag("messaging.system",              "servicebus");
            activity?.SetTag("messaging.destination",         requestAudience.Endpoint.EntityName);
            activity?.SetTag("messaging.reply_to",            replyAudience.Endpoint.EntityName);
            activity?.SetTag("messaging.message_id",          context.MessageId);
            activity?.SetTag("messaging.session_id",          context.SessionId);
            activity?.SetTag("messaging.request_reply.mode",  enableOffline ? "offline" : "sync");

            // Envoi via le cache de senders (ServiceBusSenderCache, pas de CreateSender ad-hoc)
            var sender = _senderCache.GetOrCreate(_client, requestAudience.Endpoint.EntityName);
            var sw = Stopwatch.StartNew();
            try
            {
                await sender.SendMessageAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("exception.type",    ex.GetType().FullName);
                activity?.SetTag("exception.message", ex.Message);
                throw;
            }

            // Réception de la réponse via session sur l'endpoint de réponse (queue distincte)
            var timeout = enableOffline ? TimeSpan.FromSeconds(5) : _providerOptions.ReplyTimeout;
            await using var receiver = await _client.AcceptSessionAsync(
                replyAudience.Endpoint.EntityName,
                context.SessionId,
                cancellationToken: cancellationToken);

            var received = await receiver.ReceiveMessageAsync(timeout, cancellationToken);
            sw.Stop();

            if (received == null)
            {
                activity?.SetTag("messaging.request_reply.result",      "timeout");
                activity?.SetTag("messaging.request_reply.duration_ms", sw.ElapsedMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogWarning(
                    "Pas de réponse R/R (timeout {Timeout:g}) MessageId={MessageId}",
                    timeout, context.MessageId);
                return null;
            }

            // Désérialise comme MessageTransitContext<TResponse> — deux types params distincts (TRequest ≠ TResponse)
            var deserResult = _serializer.DeserializeSafe<MessageTransitContext<TResponse>>(
                received.Body.ToString());

            await receiver.CompleteMessageAsync(received, cancellationToken);

            if (!deserResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Échec désérialisation réponse R/R ({Reason}) MessageId={MessageId}",
                    deserResult.FailureReason, context.MessageId);
                activity?.SetStatus(ActivityStatusCode.Error, deserResult.FailureReason.ToString());
                return null;
            }

            activity?.SetTag("messaging.request_reply.result",      "received");
            activity?.SetTag("messaging.request_reply.duration_ms", sw.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);

            var replyContext = deserResult.Value!;
            if (string.IsNullOrWhiteSpace(replyContext.CorrelationId))
                replyContext.CorrelationId = context.CorrelationId;

            // Journal pattern A5 — un échec de journal ne remonte jamais vers le caller
            (string? consumer, string? action) = ExtractProperties(properties);
            var journalEntry = JournalEntry.ForRequestReply(
                consumer            ?? "(none)",
                action              ?? "(none)",
                context.MessageId   ?? string.Empty,
                context.SessionId   ?? string.Empty,
                requestAudience.Target ?? string.Empty,
                200,
                context.SessionId,
                _config.AppSettings?.ApplicationName,
                _systemClock.UtcNow.UtcDateTime);

            try { await _journal.WriteRecordAsync(journalEntry, cancellationToken); }
            catch (Exception jEx)
            {
                _logger.LogWarning(jEx, "Journal failed (R/R) MessageId={MessageId}", context.MessageId);
            }

            return replyContext;
        }

        private static (string? consumer, string? action) ExtractProperties(Dictionary<string, object>? props)
        {
            if (props == null) return (null, null);
            props.TryGetValue(MessagePropertyKeys.Consumer, out var c);
            props.TryGetValue(MessagePropertyKeys.Action,   out var a);
            return (c as string, a as string);
        }
    }
}
