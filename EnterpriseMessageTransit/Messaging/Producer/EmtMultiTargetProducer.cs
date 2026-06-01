using Microsoft.Extensions.DependencyInjection;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Implémentation interne de <see cref="IMultiTargetProducer{TBase}"/>.
    /// Résout <c>IMessageProducer&lt;TPayload&gt;</c> depuis le conteneur DI
    /// et délègue chaque publication au producer correspondant.
    /// </summary>
    internal sealed class EmtMultiTargetProducer<TBase> : IMultiTargetProducer<TBase>
        where TBase : class
    {
        private readonly IServiceProvider _sp;
        private readonly IReadOnlyDictionary<Type, string> _targets;

        internal EmtMultiTargetProducer(
            IServiceProvider sp,
            IReadOnlyDictionary<Type, string> targets)
        {
            _sp      = sp      ?? throw new ArgumentNullException(nameof(sp));
            _targets = targets ?? throw new ArgumentNullException(nameof(targets));
        }

        /// <inheritdoc/>
        public Task<MessageTransitContext<MessageTransitResponse>> PublishAsync<TPayload>(
            MessageTransitContext<TPayload> context,
            PublishOptions? options = null,
            CancellationToken cancellationToken = default)
            where TPayload : class, TBase
        {
            var producer = _sp.GetRequiredService<IMessageProducer<TPayload>>();
            return producer.PublishAsync(context, options, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> PublishMixedBatchAsync(
            IEnumerable<MessageTransitContext<TBase>> contexts,
            PublishOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(contexts);

            var ids = new List<string>();

            foreach (var ctx in contexts)
            {
                var payloadType = ctx.Message?.GetType()
                    ?? throw new InvalidOperationException(
                        $"Un contexte dans PublishMixedBatchAsync a un Message null. " +
                        $"Chaque contexte doit avoir un Message non-null pour que le type puisse être résolu.");

                if (!_targets.ContainsKey(payloadType))
                    throw new InvalidOperationException(
                        $"Aucune cible enregistrée pour le type '{payloadType.Name}'. " +
                        $"Types enregistrés : {string.Join(", ", _targets.Keys.Select(t => t.Name))}.");

                // Résolution du producer via le conteneur DI (résolu par typeof(TPayload))
                var producerType = typeof(IMessageProducer<>).MakeGenericType(payloadType);
                var producer = _sp.GetRequiredService(producerType);

                // Construction d'un MessageTransitContext<TPayload> typé depuis le contexte TBase.
                // Les propriétés sont copiées ; Message est casté vers TPayload (type runtime confirmé).
                var typedCtx = BuildTypedContext(ctx, payloadType);

                // Dispatch via dynamic pour éviter la réflexion sur la signature générique
                dynamic dynamicProducer = producer;
                dynamic dynamicCtx      = typedCtx;

                MessageTransitContext<MessageTransitResponse> result =
                    await dynamicProducer.PublishAsync(dynamicCtx, options, cancellationToken);

                ids.Add(result.MessageId ?? string.Empty);
            }

            return ids;
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Construit un <c>MessageTransitContext&lt;TPayload&gt;</c> depuis un
        /// <c>MessageTransitContext&lt;TBase&gt;</c> en copiant les propriétés standard.
        /// </summary>
        private static object BuildTypedContext(MessageTransitContext<TBase> source, Type payloadType)
        {
            var ctxType = typeof(MessageTransitContext<>).MakeGenericType(payloadType);
            dynamic typed = Activator.CreateInstance(ctxType)
                ?? throw new InvalidOperationException($"Impossible d'instancier MessageTransitContext<{payloadType.Name}>.");

            typed.MessageId     = source.MessageId;
            typed.CorrelationId = source.CorrelationId;
            typed.SessionId     = source.SessionId;
            typed.MessageType   = source.MessageType ?? payloadType.AssemblyQualifiedName;
            typed.Variables     = source.Variables;
            typed.Tokens        = source.Tokens;
            typed.Attempt       = source.Attempt;
            typed.Message       = source.Message; // runtime type est TPayload

            return typed;
        }
    }
}
