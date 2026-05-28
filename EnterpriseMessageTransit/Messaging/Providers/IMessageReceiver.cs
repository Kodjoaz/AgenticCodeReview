namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Responsabilité unique : binding du message entrant dans le contexte d'invocation.
    /// Séparé de la désérialisation (<see cref="IMessageDeserializer"/>) pour permettre aux
    /// adaptateurs de ne pas implémenter la logique de désérialisation.
    /// </summary>
    public interface IMessageReceiver
    {
        void SetInvocationMetadata(string? target, string? consumer, string? action);
        void BindContext(object message, object actions);
        void BindContext(IMessageTransit message, object actions);
    }
}
