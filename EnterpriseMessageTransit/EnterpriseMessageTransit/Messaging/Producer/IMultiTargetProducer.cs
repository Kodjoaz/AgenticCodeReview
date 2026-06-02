namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Producer multi-cible avec routage automatique par type de message.
    /// <para>
    /// Élimine le boilerplate du pattern Strategy maison (CarProducer, HotelProducer…).
    /// Chaque TPayload est routé vers la cible logique déclarée via
    /// <c>AddMultiTargetProducer&lt;TBase&gt;(b =&gt; b.AddTarget&lt;TPayload&gt;("target"))</c>.
    /// </para>
    /// </summary>
    /// <typeparam name="TBase">
    /// Type de base commun à tous les messages (interface marqueur ou classe abstraite).
    /// Peut être <c>object</c> si les messages n'ont pas de type commun.
    /// </typeparam>
    public interface IMultiTargetProducer<TBase> where TBase : class
    {
        /// <summary>
        /// Publie un message TPayload vers la cible logique enregistrée pour ce type.
        /// La résolution de cible se fait par <c>typeof(TPayload)</c> — zéro magic string.
        /// </summary>
        Task<MessageTransitContext<MessageTransitResponse>> PublishAsync<TPayload>(
            MessageTransitContext<TPayload> context,
            PublishOptions? options = null,
            CancellationToken cancellationToken = default)
            where TPayload : class, TBase;

        /// <summary>
        /// Publie une collection hétérogène de messages.
        /// Chaque élément est dispatché vers la cible correspondant au type réel de
        /// <c>context.Message</c>. Les items sont traités séquentiellement ; les
        /// MessageIds sont retournés dans l'ordre d'entrée.
        /// </summary>
        Task<IReadOnlyList<string>> PublishMixedBatchAsync(
            IEnumerable<MessageTransitContext<TBase>> contexts,
            PublishOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
