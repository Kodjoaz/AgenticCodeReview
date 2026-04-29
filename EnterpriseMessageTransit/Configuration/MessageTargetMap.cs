using Microsoft.Extensions.Options;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    /// <summary>
    /// Options de liaison TMessage → target, peuplées par services.AddProducer&lt;T&gt;("target").
    /// Chaque appel à AddProducer accumule une entrée dans le dictionnaire.
    /// </summary>
    public class MessageTargetMapOptions
    {
        internal Dictionary<Type, string> Mappings { get; } = new();

        /// <summary>
        /// Enregistre la liaison TMessage → target.
        /// </summary>
        public void Map<TMessage>(string target) where TMessage : class
        {
            ArgumentNullException.ThrowIfNull(target);
            Mappings[typeof(TMessage)] = target;
        }
    }

    /// <summary>
    /// Implémentation singleton de IMessageTargetMap, alimentée par les options DI.
    /// Résout le target associé à un type de message au runtime sans passer par le constructeur.
    /// </summary>
    public sealed class MessageTargetMap : IMessageTargetMap
    {
        private readonly IReadOnlyDictionary<Type, string> _mappings;

        public MessageTargetMap(IOptions<MessageTargetMapOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options?.Value);
            _mappings = new Dictionary<Type, string>(options.Value.Mappings);
        }

        /// <inheritdoc/>
        public string? ResolveTarget<TMessage>() where TMessage : class
            => _mappings.TryGetValue(typeof(TMessage), out var target) ? target : null;

        /// <inheritdoc/>
        public string? ResolveTarget(Type messageType)
            => _mappings.TryGetValue(messageType, out var target) ? target : null;
    }
}
