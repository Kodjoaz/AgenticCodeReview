namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    /// <summary>
    /// Résolution du target par type de message.
    /// Enregistré au démarrage via services.AddProducer&lt;TMessage&gt;("target").
    /// Permet d'éliminer le passage du target dans les constructeurs et les appels de méthode.
    /// </summary>
    public interface IMessageTargetMap
    {
        /// <summary>
        /// Résout le target associé au type TMessage, ou null si aucune liaison enregistrée.
        /// </summary>
        string? ResolveTarget<TMessage>() where TMessage : class;

        /// <summary>
        /// Résout le target associé à un type de message, ou null si aucune liaison enregistrée.
        /// </summary>
        string? ResolveTarget(Type messageType);
    }
}
