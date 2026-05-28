using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Responsabilité unique : désérialisation du message entrant en un résultat structuré.
    /// Séparé de <see cref="IMessageReceiver"/> (binding) pour permettre aux adaptateurs
    /// légers de ne pas implémenter cette logique.
    /// </summary>
    public interface IMessageDeserializer
    {
        /// <summary>
        /// Désérialise le message entrant. Utiliser <see cref="DeserializationResult{T}.IsSuccess"/>
        /// pour décider du settlement.
        /// </summary>
        DeserializationResult<MessageTransitContext<TMessage>> DeserializeMessageSafe<TMessage>() where TMessage : class;
    }
}
