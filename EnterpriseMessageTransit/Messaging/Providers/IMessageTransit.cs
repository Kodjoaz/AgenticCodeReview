namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Abstraction du message reçu depuis le broker.
    /// Expose les propriétés nécessaires aux consumers sans les coupler à Service Bus.
    ///
    /// Phase 2 (P2-C4) : enrichi avec DeliveryCount, EnqueuedTime, CorrelationId,
    /// ReplyTo et ApplicationProperties pour éliminer les casts vers
    /// AzureFunctionMessageTransit dans les consumers applicatifs.
    /// </summary>
    public interface IMessageTransit
    {
        string MessageId { get; }
        string Content { get; }
        long SequenceNumber { get; }
        string? SessionId { get; }

        /// <summary>Nombre de tentatives de livraison du message (incrémenté par Service Bus).</summary>
        int DeliveryCount { get; }

        /// <summary>Heure à laquelle le message a été mis en file d'attente par le producer.</summary>
        DateTimeOffset EnqueuedTime { get; }

        /// <summary>Identifiant de corrélation applicatif (optionnel — défini par le producer).</summary>
        string? CorrelationId { get; }

        /// <summary>Entité de réponse (optionnel — utilisé dans le pattern Request/Reply).</summary>
        string? ReplyTo { get; }

        /// <summary>
        /// Propriétés applicatives personnalisées attachées au message.
        /// Accès en lecture seule — ne pas caster vers AzureFunctionMessageTransit pour modifier.
        /// </summary>
        IReadOnlyDictionary<string, object> ApplicationProperties { get; }
    }
}
