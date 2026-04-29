using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer
{
    /// <summary>
    /// Base Consumer refonte – centralise:
    /// - Désérialisation / binding (BindContext / DeserializeMessage)
    /// - Actions message (Complete / DeadLetter / Retry)
    /// - Routage Saga (RouteToNextStageAsync) + Topics (Consumer[.Action])
    /// - Validation d’étapes (AssertNextStageExists / AssertLastStage)
    /// - Idempotence finalisation (__FinalStageCompleted)
    /// </summary>
    /// <typeparam name="TMessage">Représente le contenu de l'événement</typeparam>
    public abstract class BaseConsumer<TMessage> : BaseMessageTransit<TMessage>, IMessageConsumer<TMessage> where TMessage : class
    {
        private readonly string? _configuredTarget;
        private readonly string? _consumer;
        private readonly string? _action;

        protected readonly IMessagingProvider MessagingProvider;

        /// <summary>
        /// Constructeur avec injections invoquand le constructeur de l'objet de base de message transit
        /// </summary>
        /// <param name="messagingProvider"></param>
        /// <param name="logger"></param>
        /// <param name="config"></param>
        /// <param name="serializer"></param>
        /// <param name="storageProvider"></param>
        /// <param name="target">Optionel,utilisé pour résoudre l’audience, peut être surchargé à l'appel (paramètre target de PublishAsync/GetResponseAsync).</param>
        /// <param name="consumer"></param>
        /// <param name="action"></param>
        /// <exception cref="ArgumentNullException">Injection obligatoire manquante</exception>
        protected BaseConsumer(
            IMessagingProvider messagingProvider,
            ILogger logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            string? target = null,
            string? consumer = null,
            string? action = null)
            : base(logger, config, serializer, storageProvider)
        {
            MessagingProvider = messagingProvider ?? throw new ArgumentNullException(nameof(messagingProvider));
            _configuredTarget = string.IsNullOrWhiteSpace(target) ? null : target;
            _consumer = consumer;
            _action = action;
        }

        protected void Stamp() => MessagingProvider.SetInvocationMetadata(_configuredTarget, _consumer, _action);

        public abstract Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<TMessage> context,
            CancellationToken cancellationToken);

        #region Résolution Stage
        private string ResolveEffectiveCurrentStage<TAnyMessage>(MessageTransitContext<TAnyMessage> ctx) where TAnyMessage : class
        {
            if (!string.IsNullOrWhiteSpace(ctx?.CurrentStage))
            {
                return ctx.CurrentStage!;
            }

            if (!string.IsNullOrWhiteSpace(_configuredTarget))
            {
                List<EndpointSettings>? itin = Config.AppSettings?.Itinerary;
                if (itin != null)
                {
                    EndpointSettings? aud = itin.Find(a => string.Equals(a.Target, _configuredTarget, StringComparison.OrdinalIgnoreCase));
                    if (aud?.Endpoint?.EntityType == MessagingEntityType.Topic &&
                        aud.Endpoint.Subscription != null &&
                        !string.IsNullOrWhiteSpace(aud.Endpoint.Subscription.Consumer))
                    {
                        return string.IsNullOrWhiteSpace(aud.Endpoint.Subscription.Action)
                            ? aud.Endpoint.Subscription.Consumer
                            : $"{aud.Endpoint.Subscription.Consumer}.{aud.Endpoint.Subscription.Action}";
                    }
                }
                return _configuredTarget!;
            }

            throw new TransitItineraryException("ResolveEffectiveCurrentStage: Current stage not found.");
        }

        private int FindIndexFromStage(string effectiveStage)
        {
            var itin = Config.AppSettings?.Itinerary
                ?? throw new TransitItineraryException("Itinerary missing.");

            // 1) Match direct sur Target
            int idx = itin.FindIndex(a => string.Equals(a.Target, effectiveStage, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                return idx;
            }

            // 2) Si stage de forme Consumer.Action, tester la partie base (avant '.')
            var dot = effectiveStage.IndexOf('.');
            if (dot > 0)
            {
                var baseSeg = effectiveStage[..dot];
                idx = itin.FindIndex(a => string.Equals(a.Target, baseSeg, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    return idx;
                }
            }

            // 3) Mapper via Subscription.Consumer[.Action] (Action optionnelle)
            for (int i = 0; i < itin.Count; i++)
            {
                var sub = itin[i].Endpoint?.Subscription;
                if (sub == null || string.IsNullOrWhiteSpace(sub.Consumer))
                {
                    continue;
                }

                var stageFromSub = string.IsNullOrWhiteSpace(sub.Action)
                    ? sub.Consumer
                    : $"{sub.Consumer}.{sub.Action}";

                if (string.Equals(stageFromSub, effectiveStage, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                // Fallback: si effectiveStage ne contient pas d’action, comparer Consumer seul
                if (dot < 0 && string.Equals(sub.Consumer, effectiveStage, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            throw new TransitItineraryException($"Stage '{effectiveStage}' not found in itinerary.");
        }
        #endregion

        #region Désérialisation
        /// <summary>
        /// Variante asynchrone recommandée (évite .GetAwaiter().GetResult() et risques de deadlock).
        /// Désérialise le message et hydrate le claim-check si nécessaire.
        /// </summary>
        public async Task<MessageTransitContext<TAnyMessage>?> DeserializeMessageAsync<TAnyMessage>(CancellationToken cancellationToken = default) where TAnyMessage : class
        {
            Stamp();
            MessageTransitContext<TAnyMessage>? ctx = MessagingProvider.DeserializeMessage<TAnyMessage>();
            AlignStage(ctx);

            // If claim-check applied (message token present and no inlined message), try to download and hydrate.
            if (ctx != null && ctx.Message == null)
            {
                var msgToken = ctx.GetMessageToken();
                if (msgToken != null && !string.IsNullOrWhiteSpace(msgToken.Reference))
                {
                    try
                    {
                        using var stream = await StorageProvider.DownloadAsync(msgToken.Reference, cancellationToken);
                        using var sr = new System.IO.StreamReader(stream);
                        var payload = sr.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(payload))
                        {
                            var deserialized = Serializer.Deserialize<TAnyMessage>(payload);
                            ctx.Message = deserialized;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "DeserializeMessageAsync: failed to download claim-check token for MessageId={MessageId}", ctx.MessageId);
                    }
                }
            }

            return ctx;
        }

        /// <summary>
        /// Variante synchrone (héritée). Préférer DeserializeMessageAsync pour éviter les deadlocks.
        /// ⚠️ Utilise .GetAwaiter().GetResult() en interne — utiliser avec prudence.
        /// </summary>
        [Obsolete("Utiliser DeserializeMessageAsync à la place pour éviter les risques de deadlock.", false)]
        public MessageTransitContext<TAnyMessage>? DeserializeMessage<TAnyMessage>() where TAnyMessage : class
        {
            return DeserializeMessageAsync<TAnyMessage>(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Variante asynchrone recommandée.
        /// </summary>
        public async Task<bool> TryDeserializeMessageAsync<TAnyMessage>(CancellationToken cancellationToken = default) where TAnyMessage : class
        {
            var ctx = await DeserializeMessageAsync<TAnyMessage>(cancellationToken);
            return ctx != null;
        }

        /// <summary>
        /// Variante synchrone (héritée). Préférer TryDeserializeMessageAsync.
        /// ⚠️ Utilise .GetAwaiter().GetResult() en interne — utiliser avec prudence.
        /// </summary>
        [Obsolete("Utiliser TryDeserializeMessageAsync à la place pour éviter les risques de deadlock.", false)]
        public bool TryDeserializeMessage<TAnyMessage>(out MessageTransitContext<TAnyMessage>? context) where TAnyMessage : class
        {
            Stamp();
            context = MessagingProvider.DeserializeMessage<TAnyMessage>();
            AlignStage(context);
            return context != null;
        }

        private void AlignStage<TAnyMessage>(MessageTransitContext<TAnyMessage>? ctx) where TAnyMessage : class
        {
            if (ctx == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(ctx.CurrentStage))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_configuredTarget))
            {
                try
                {
                    ctx.SetCurrentStage(ResolveEffectiveCurrentStage(ctx));
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "AlignStage: failed to resolve effective current stage, falling back to configured target '{ConfiguredTarget}'.", _configuredTarget);
                    ctx.SetCurrentStage(_configuredTarget!);
                }
            }
        }

        public void BindContext(object message, object actions)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            Stamp();
            // Prefer the typed BindContext overload when the caller provides an IMessageTransit
            if (message is IMessageTransit mt)
            {
                MessagingProvider.BindContext(mt, actions);
            }
            else
            {
                MessagingProvider.BindContext(message, actions);
            }
        }
        #endregion

        #region Actions message
        /// <summary>
        /// Completion saga-aware.
        /// - ctx null: completion simple.
        /// - ctx final: pose __FinalStageCompleted (idempotent).
        /// </summary>
        public async Task CompleteMessageAsync<TCurrent>(
            MessageTransitContext<TCurrent>? ctx = null,
            CancellationToken ct = default) where TCurrent : class
        {
            Stamp();

            if (ctx != null)
            {
                try
                {
                    var effectiveStage = ResolveEffectiveCurrentStage(ctx);
                    var itin = Config.AppSettings?.Itinerary;
                    if (itin != null && itin.Count > 0)
                    {
                        int idx = FindIndexFromStage(effectiveStage);
                        if (idx == itin.Count - 1)
                        {
                            ctx.Variables ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            string cle = "__FinalStageCompleted";
                            if (!ctx.Variables.ContainsKey(cle))
                            {
                                Logger.LogInformation("Saga finalisé (stage={Stage}) MessageId={MessageId}", effectiveStage, ctx.MessageId);
                                ctx.Variables[cle] = true;
                            }
                            else
                            {
                                Logger.LogDebug("CompleteMessageAsync: final déjà marqué.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "CompleteMessageAsync: failed to mark final stage for MessageId={MessageId}.", ctx.MessageId);
                    // keep execution path: do not rethrow here to avoid breaking completion, but ensure exception is logged
                }
            }

            await MessagingProvider.CompleteMessageAsync(ct);
        }

        public Task CompleteMessageAsync(CancellationToken ct = default) =>
            CompleteMessageAsync<object>(null, ct);

        public Task DeadLetterMessageAsync(Exception ex, CancellationToken ct = default)
        {
            Stamp();
            return MessagingProvider.DeadLetterMessageAsync(ex, ct);
        }

        protected Task ImmediateRetryAsync(ImmediateRetryException ex, CancellationToken ct = default)
        {
            Stamp();
            return MessagingProvider.ImmediateRetryAsync(ex, ct);
        }

        protected Task ExponentialRetryAsync(ExponentialRetryException ex, CancellationToken ct = default)
        {
            Stamp();
            return MessagingProvider.ExponentialRetryAsync(ex, ct);
        }
        #endregion

        #region Dispatch Saga
        protected async Task<bool> RouteToNextStageAsync<TCurrent, TNext>(
            MessageTransitContext<TCurrent> currentContext,
            TNext nextPayload,
            CancellationToken ct)
            where TCurrent : class
            where TNext : class
        {
            if (currentContext == null)
            {
                throw new ArgumentNullException(nameof(currentContext));
            }

            if (nextPayload == null)
            {
                throw new ArgumentNullException(nameof(nextPayload));
            }

            Stamp();

            var effectiveStage = ResolveEffectiveCurrentStage(currentContext);
            var itin = Config.AppSettings?.Itinerary
                ?? throw new TransitItineraryException("RouteToNextStageAsync: Itinerary missing.");
            var idx = FindIndexFromStage(effectiveStage);

            // Dernière étape: juste completion idempotente
            if (idx == itin.Count - 1)
            {
                await CompleteMessageAsync(currentContext, ct);
                return false;
            }

            EndpointSettings nextAud = itin[idx + 1];
            string target = nextAud.Target ?? throw new TransitItineraryException($"nextAud.Target absent pour '{nextAud}'.");
            TransportSettings endpoint = nextAud.Endpoint ?? throw new TransitItineraryException($"Endpoint manquant pour '{nextAud.Target}'.");
            if (string.IsNullOrWhiteSpace(endpoint.EntityName))
            {
                throw new TransitItineraryException($"Endpoint.EntityName absent pour '{nextAud.Target}'.");
            }


            string nextStage = endpoint.EntityType == MessagingEntityType.Topic
                ? GetTopicStage(endpoint)
                : target;

            string nextConsumer = endpoint.Subscription?.Consumer ?? target;
            string? nextAction = endpoint.Subscription?.Action;

            var nextCtx = new MessageTransitContext<TNext>
            {
                MessageId = currentContext.MessageId,
                SessionId = currentContext.SessionId,
                SequenceNumber = currentContext.SequenceNumber,
                Attempt = currentContext.Attempt,
                Message = nextPayload,
                Variables = currentContext.Variables != null
                    ? new Dictionary<string, object>(currentContext.Variables, StringComparer.OrdinalIgnoreCase)
                    : null
            };
            nextCtx.SetCurrentStage(nextStage);

            var props = new Dictionary<string, object>
            {
                ["EntityType"] = endpoint.EntityType.ToString(),
                ["EntityName"] = endpoint.EntityName,
                ["ReferralCount"] = 0,
                ["Consumer"] = nextConsumer
            };
            if (!string.IsNullOrWhiteSpace(nextAction))
            {
                props["Action"] = nextAction;
            }

            if (endpoint.EntityType == MessagingEntityType.Queue)
            {
                props["PreviousStage"] = effectiveStage;
            }

            var options = new MessagingOptions { Target = nextStage, Properties = props };

            Logger.LogInformation(
                "Saga dispatch: Current={Current} -> Next={Next} Entity={Entity}/{Type} Consumer={Consumer} Action={Action} MessageId={MessageId}",
                effectiveStage, nextStage, endpoint.EntityName, endpoint.EntityType,
                nextConsumer, nextAction ?? "(none)", currentContext.MessageId);

            await MessagingProvider.SendAsync(nextCtx, options, ct);
            await CompleteMessageAsync(currentContext, ct);
            return true;
        }

        protected async Task<bool> RouteToNextStageAsync<TCurrent>(
            MessageTransitContext<TCurrent> currentContext,
            string expectedNextTarget,
            CancellationToken ct)
            where TCurrent : class
        {
            if (currentContext == null)
            {
                throw new ArgumentNullException(nameof(currentContext));
            }

            if (currentContext.Message == null)
            {
                throw new TransitItineraryException("RouteToNextStageAsync (same message): payload null.");
            }

            var effectiveStage = ResolveEffectiveCurrentStage(currentContext);
            AssertNextStageExistsFromStage(effectiveStage, expectedNextTarget);
            return await RouteToNextStageAsync(currentContext, currentContext.Message, ct);
        }

        protected async Task<bool> RouteToNextStageAsync<TCurrent, TNext>(
            MessageTransitContext<TCurrent> currentContext,
            string expectedNextTarget,
            Func<MessageTransitContext<TCurrent>, TNext> buildNextPayload,
            CancellationToken ct)
            where TCurrent : class
            where TNext : class
        {
            if (currentContext == null)
            {
                throw new ArgumentNullException(nameof(currentContext));
            }

            if (buildNextPayload == null)
            {
                throw new ArgumentNullException(nameof(buildNextPayload));
            }

            var effectiveStage = ResolveEffectiveCurrentStage(currentContext);
            AssertNextStageExistsFromStage(effectiveStage, expectedNextTarget);
            var payload = buildNextPayload(currentContext);
            return await RouteToNextStageAsync(currentContext, payload, ct);
        }

        protected Task<bool> RouteToNextStageAsync<TCurrent>(
            MessageTransitContext<TCurrent> currentContext,
            CancellationToken ct)
            where TCurrent : class
        {
            if (currentContext == null)
            {
                throw new ArgumentNullException(nameof(currentContext));
            }

            if (currentContext.Message == null)
            {
                throw new TransitItineraryException("RouteToNextStageAsync (same type): payload null.");
            }

            return RouteToNextStageAsync(currentContext, currentContext.Message, ct);
        }

        private static string GetTopicStage(TransportSettings endpoint)
        {
            if (endpoint.Subscription == null || string.IsNullOrWhiteSpace(endpoint.Subscription.Consumer))
            {
                throw new TransitItineraryException("GetTopicStage: Subscription.Consumer required.");
            }

            return string.IsNullOrWhiteSpace(endpoint.Subscription.Action)
                ? endpoint.Subscription.Consumer
                : $"{endpoint.Subscription.Consumer}.{endpoint.Subscription.Action}";
        }

        private void AssertNextStageExistsFromStage(string effectiveStage, string expectedNextTarget)
        {
            if (string.IsNullOrWhiteSpace(expectedNextTarget))
            {
                throw new ArgumentNullException(nameof(expectedNextTarget));
            }

            var itin = Config.AppSettings?.Itinerary
                ?? throw new TransitItineraryException("AssertNextStageExists: Itinerary missing.");

            int idx = FindIndexFromStage(effectiveStage);
            if (idx == itin.Count - 1)
            {
                throw new TransitItineraryException($"AssertNextStageExists: fin d’itinéraire après '{effectiveStage}'.");
            }

            var realNext = itin[idx + 1].Target;
            if (!string.Equals(realNext, expectedNextTarget, StringComparison.OrdinalIgnoreCase))
            {
                throw new TransitItineraryException($"AssertNextStageExists: prochaine réelle '{realNext}' ≠ attendue '{expectedNextTarget}'.");
            }
        }
        #endregion

        #region Validation finale
        protected void AssertLastStage()
        {
            var baseStage = _configuredTarget ?? throw new TransitItineraryException("AssertLastStage: target non défini.");
            var itin = Config.AppSettings?.Itinerary
                ?? throw new TransitItineraryException("AssertLastStage: Itinerary missing.");
            int idx = FindIndexFromStage(baseStage);
            if (idx != itin.Count - 1)
            {
                var next = itin[idx + 1].Target;
                throw new TransitItineraryException($"AssertLastStage: '{baseStage}' n'est pas la dernière étape. Prochaine: '{next}'.");
            }
        }

        protected bool IsLastStage()
        {
            if (_configuredTarget == null)
            {
                return false;
            }

            var itin = Config.AppSettings?.Itinerary;
            if (itin == null || itin.Count == 0)
            {
                return false;
            }

            return string.Equals(itin[^1].Target, _configuredTarget, StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
