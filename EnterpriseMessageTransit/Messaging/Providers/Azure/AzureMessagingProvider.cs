using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    public class AzureMessagingProvider : IMessagingProvider
    {
        private readonly IMessagingAdapter _adapter;
        private readonly IMessageSerializer _serializer;
        private readonly ServiceBusClient _client;
        private readonly IMessageTransitConfigurationService _config;
        private readonly IEndpointResolver _endpointResolver;
        private readonly ILogger<AzureMessagingProvider> _logger;
        private readonly ServiceBusSenderCache _senderCache;
        private readonly AzureServiceBusProviderOptions _providerOptions;
        private readonly CircuitBreakerManager _circuitBreaker;

        public AzureMessagingProvider(
            IMessagingAdapter adapter,
            IMessageSerializer serializer,
            ServiceBusClient client,
            IMessageTransitConfigurationService config,
            IEndpointResolver endpointResolver,
            ILogger<AzureMessagingProvider> logger,
            ServiceBusSenderCache senderCache,
            AzureServiceBusProviderOptions providerOptions,
            CircuitBreakerManager circuitBreaker)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _senderCache = senderCache ?? throw new ArgumentNullException(nameof(senderCache));
            _providerOptions = providerOptions ?? new AzureServiceBusProviderOptions();
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        }

        // A2 — SetInvocationMetadata délègue uniquement à l'adapter.
        // Plus d'état mutable _target/_consumer/_action dans le provider :
        // les valeurs voyagent dans MessagingOptions (Producer) ou via BindContext (Consumer).
        public void SetInvocationMetadata(string? target, string? consumer, string? action)
        {
            _adapter.SetInvocationMetadata(target, consumer, action);
        }

        public EndpointSettings Resolve(string? target)
        {
            // A2 — résolution directe depuis le target fourni, sans fallback sur un état d'instance.
            if (_endpointResolver.TryResolve(target, null, null, out var aud) && aud != null)
            {
                return aud;
            }
            throw new InvalidOperationException($"EndpointSettings non résolue (target='{target}').");
        }

        // A3 — BindContext délègue directement à l'adapter.
        // Le casting vers ServiceBusMessageActions (Microsoft.Azure.Functions.Worker) est encapsulé
        // dans AzureFunctionMessagingAdapter — ce provider générique n'en dépend plus.
        public void BindContext(object message, object actions)
        {
            _adapter.BindContext(message, actions);
        }

        public void BindContext(IMessageTransit message, object actions)
        {
            _adapter.BindContext(message, actions);
        }

        private static bool IsFatalSendException(Exception ex)
        {
            if (ex is ObjectDisposedException) return true;
            if (ex is ServiceBusException sbEx && !sbEx.IsTransient) return true;
            return false;
        }

        public async Task SendAsync<T>(MessageTransitContext<T> context, MessagingOptions options, CancellationToken cancellationToken = default) where T : class
        {
            var audience = Resolve(options.Target);
            var retryPolicy = audience.Endpoint.SendRetry ?? ProducerSendRetryPolicy.Default;

            var payload = context.SerializedPayload ?? _serializer.Serialize(context);
            var message = new ServiceBusMessage(payload)
            {
                MessageId = context.MessageId ?? Guid.NewGuid().ToString(),
                SessionId = options.EnableSession ? context.SessionId ?? context.MessageId : null
            };

            if (options.Properties != null)
            {
                foreach (var kvp in options.Properties)
                {
                    message.ApplicationProperties[kvp.Key] = kvp.Value ?? string.Empty;
                }
            }

            await SendSingleWithRetryAsync(
                _senderCache.GetOrCreate(_client, audience.Endpoint.EntityName),
                message,
                audience.Endpoint.EntityName,
                retryPolicy,
                cancellationToken);
        }

        public async Task SendBatchAsync<T>(IEnumerable<MessageTransitContext<T>> contexts, MessagingOptions options, CancellationToken cancellationToken = default) where T : class
        {
            var audience = Resolve(options.Target);
            var entityName = audience.Endpoint.EntityName;
            var retryPolicy = audience.Endpoint.SendRetry ?? ProducerSendRetryPolicy.Default;
            var sender = _senderCache.GetOrCreate(_client, entityName);

            // Construction de la file de messages Service Bus
            var pending = new Queue<ServiceBusMessage>();
            foreach (var ctx in contexts)
            {
                var payload = ctx.SerializedPayload ?? _serializer.Serialize(ctx);
                var msg = new ServiceBusMessage(payload)
                {
                    MessageId = ctx.MessageId ?? Guid.NewGuid().ToString(),
                    SessionId = ctx.SessionId
                };
                if (options.Properties != null)
                {
                    foreach (var kvp in options.Properties)
                    {
                        msg.ApplicationProperties[kvp.Key] = kvp.Value ?? string.Empty;
                    }
                }
                pending.Enqueue(msg);
            }

            // C2 — envoi en batches séquentiels sans perte silencieuse.
            // Chaque message est traité jusqu'à épuisement de la file.
            // Chaque batch de 256 Ko est envoyé atomiquement ; plusieurs batches ne sont pas atomiques entre eux.
            while (pending.Count > 0)
            {
                using var batch = await sender.CreateMessageBatchAsync(cancellationToken);
                bool batchHasMessages = false;

                while (pending.Count > 0)
                {
                    var msg = pending.Peek();
                    if (batch.TryAddMessage(msg))
                    {
                        pending.Dequeue();
                        batchHasMessages = true;
                    }
                    else if (!batchHasMessages)
                    {
                        // Message trop grand pour un batch vide → envoi unitaire avec retry
                        pending.Dequeue();
                        await SendSingleWithRetryAsync(sender, msg, entityName, retryPolicy, cancellationToken);
                    }
                    else
                    {
                        // Batch courant plein → l'envoyer et recommencer avec le suivant
                        break;
                    }
                }

                if (batchHasMessages)
                {
                    await SendBatchWithRetryAsync(sender, batch, entityName, retryPolicy, cancellationToken);
                }
            }
        }

        public async Task<MessageTransitContext<TMessage>?> RequestReplyAsync<TMessage>(MessageTransitContext<TMessage> context, MessagingOptions options, CancellationToken cancellationToken = default) where TMessage : class
        {
            // C5 — ArgumentNullException au lieu de NullReferenceException (anti-pattern CLR)
            ArgumentNullException.ThrowIfNull(context.Message, nameof(context.Message));

            var audience = Resolve(options.Target);
            var retryPolicy = audience.Endpoint.SendRetry ?? ProducerSendRetryPolicy.Default;
            context.SessionId ??= context.MessageId ?? Guid.NewGuid().ToString("N");

            var request = new ServiceBusMessage(_serializer.Serialize(context.Message))
            {
                MessageId = context.MessageId ?? Guid.NewGuid().ToString(),
                SessionId = context.SessionId,
                ReplyToSessionId = context.SessionId
            };

            if (options.Properties != null)
            {
                foreach (var kvp in options.Properties)
                {
                    request.ApplicationProperties[kvp.Key] = kvp.Value ?? "";
                }
            }

            await SendSingleWithRetryAsync(
                _senderCache.GetOrCreate(_client, audience.Endpoint.EntityName),
                request,
                audience.Endpoint.EntityName,
                retryPolicy,
                cancellationToken);

            await using var receiver = await _client.AcceptSessionAsync(audience.Endpoint.EntityName, context.SessionId, cancellationToken: cancellationToken);
            var received = await receiver.ReceiveMessageAsync(_providerOptions.ReplyTimeout, cancellationToken);
            if (received == null)
            {
                return null;
            }

            var responsePayload = _serializer.Deserialize<TMessage>(received.Body.ToString());
            var replyContext = new MessageTransitContext<TMessage>
            {
                Message = responsePayload,
                MessageId = received.MessageId,
                SessionId = received.SessionId,
                SequenceNumber = received.SequenceNumber,
                TransportMessage = new AzureFunctionMessageTransit(received),
                Tokens = context.Tokens,
                Variables = context.Variables,
                CurrentStage = audience.Target
            };
            await receiver.CompleteMessageAsync(received, cancellationToken);
            return replyContext;
        }

        /// <summary>
        /// A4 — Envoi d'un message unique avec retry selon <see cref="ProducerSendRetryPolicy"/>.
        /// En cas d'erreur fatale, remplace le sender et réessaie jusqu'à <see cref="ProducerSendRetryPolicy.MaxAttempts"/>.
        /// </summary>
        private async Task SendSingleWithRetryAsync(
            ServiceBusSender sender,
            ServiceBusMessage message,
            string entityName,
            ProducerSendRetryPolicy retryPolicy,
            CancellationToken cancellationToken)
        {
            _circuitBreaker.EnsureCircuitAllows(entityName);

            var attempt = 0;
            while (true)
            {
                try
                {
                    await sender.SendMessageAsync(message, cancellationToken);
                    _circuitBreaker.RecordSuccess(entityName);
                    return;
                }
                catch (Exception ex) when (IsFatalSendException(ex) && attempt < retryPolicy.MaxAttempts)
                {
                    attempt++;
                    _circuitBreaker.RecordFailure(entityName);
                    _logger.LogWarning(ex,
                        "Envoi échoué (tentative {Attempt}/{Max}) pour l'entité {Entity}. Remplacement du sender...",
                        attempt, retryPolicy.MaxAttempts, entityName);
                    await Task.Delay(retryPolicy.InitialDelay * attempt, cancellationToken);
                    sender = _senderCache.ReplaceSender(_client, entityName);
                }
                catch (Exception ex)
                {
                    _circuitBreaker.RecordFailure(entityName);
                    _logger.LogError(ex, "Échec définitif de l'envoi pour l'entité {Entity}", entityName);
                    throw new MessageSendException($"Échec envoi target='{entityName}'", ex);
                }
            }
        }

        /// <summary>
        /// A4 — Envoi d'un batch avec retry selon <see cref="ProducerSendRetryPolicy"/>.
        /// </summary>
        private async Task SendBatchWithRetryAsync(
            ServiceBusSender sender,
            ServiceBusMessageBatch batch,
            string entityName,
            ProducerSendRetryPolicy retryPolicy,
            CancellationToken cancellationToken)
        {
            _circuitBreaker.EnsureCircuitAllows(entityName);

            var attempt = 0;
            while (true)
            {
                try
                {
                    await sender.SendMessagesAsync(batch, cancellationToken);
                    _circuitBreaker.RecordSuccess(entityName);
                    return;
                }
                catch (Exception ex) when (IsFatalSendException(ex) && attempt < retryPolicy.MaxAttempts)
                {
                    attempt++;
                    _circuitBreaker.RecordFailure(entityName);
                    _logger.LogWarning(ex,
                        "Envoi batch échoué (tentative {Attempt}/{Max}) pour l'entité {Entity}. Remplacement du sender...",
                        attempt, retryPolicy.MaxAttempts, entityName);
                    await Task.Delay(retryPolicy.InitialDelay * attempt, cancellationToken);
                    sender = _senderCache.ReplaceSender(_client, entityName);
                }
                catch (Exception ex)
                {
                    _circuitBreaker.RecordFailure(entityName);
                    _logger.LogError(ex, "Échec définitif de l'envoi batch pour l'entité {Entity}", entityName);
                    throw new MessageSendException($"Échec envoi batch target='{entityName}'", ex);
                }
            }
        }

        public Task CompleteMessageAsync(CancellationToken cancellationToken = default) =>
            _adapter.CompleteMessageAsync(cancellationToken);
        public Task ImmediateRetryAsync(ImmediateRetryException exception, CancellationToken cancellationToken = default) =>
            _adapter.ImmediateRetryAsync(exception, cancellationToken);
        public Task ExponentialRetryAsync(ExponentialRetryException exception, CancellationToken cancellationToken = default) =>
            _adapter.ExponentialRetryAsync(exception, cancellationToken);
        public Task DeadLetterMessageAsync(Exception exception, CancellationToken cancellationToken = default) =>
            _adapter.DeadLetterMessageAsync(exception, cancellationToken);

        public MessageTransitContext<T>? DeserializeMessage<T>() where T : class
        {
            var msg = _adapter.GetMessage();
            var result = _serializer.DeserializeSafe<MessageTransitContext<T>>(msg?.Content?.ToString());
            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "DeserializeMessage failed for MessageId={MessageId}: {Reason} — {Error}",
                    msg?.MessageId, result.FailureReason, result.ErrorMessage);
                return null;
            }
            return result.Value;
        }

        public bool TryDeserialize<T>(out MessageTransitContext<T>? context) where T : class
        {
            context = DeserializeMessage<T>();
            return context != null;
        }

        // Note: sender disposal is handled by the singleton ServiceBusSenderCache.
    }
}
