namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;

/// <summary>
/// Fournisseur d'abstraction pour l'exposition de métriques custom
/// via System.Diagnostics.Metrics (compatible OpenTelemetry/Application Insights).
/// </summary>
public interface IMetricsProvider
{
    /// <summary>
    /// Incrémente le compteur de messages envoyés avec succès.
    /// </summary>
    /// <param name="entityName">Nom de l'entité Service Bus (topic, queue)</param>
    /// <param name="entityType">Type d'entité (Queue, Topic, )</param>
    void IncrementMessagesSent(string entityName, string entityType);

    /// <summary>
    /// Incrémente le compteur de messages reçus avec succès.
    /// </summary>
    /// <param name="entityName">Nom de l'entité Service Bus</param>
    /// <param name="entityType">Type d'entité</param>
    void IncrementMessagesReceived(string entityName, string entityType);

    /// <summary>
    /// Incrémente le compteur de messages envoyés vers la Dead Letter Queue.
    /// </summary>
    /// <param name="entityName">Nom de l'entité Service Bus</param>
    /// <param name="reason">Raison du dead-lettering (MaxDeliveryCountExceeded, ValidationFailed, etc.)</param>
    void IncrementMessagesDLQ(string entityName, string reason);

    /// <summary>
    /// Enregistre la durée d'envoi d'un message (en millisecondes).
    /// </summary>
    /// <param name="durationMs">Durée en millisecondes</param>
    /// <param name="entityName">Nom de l'entité Service Bus</param>
    void RecordSendDuration(double durationMs, string entityName);

    /// <summary>
    /// Enregistre la durée de réception d'un message (en millisecondes).
    /// </summary>
    /// <param name="durationMs">Durée en millisecondes</param>
    /// <param name="entityName">Nom de l'entité Service Bus</param>
    void RecordReceiveDuration(double durationMs, string entityName);

    /// <summary>
    /// Enregistre le délai appliqué lors d'un retry exponentiel (en millisecondes).
    /// </summary>
    /// <param name="delayMs">Délai en millisecondes</param>
    /// <param name="attempt">Numéro de tentative</param>
    void RecordRetryDelay(double delayMs, int attempt);

    /// <summary>
    /// Incrémente le compteur de retries immédiats (ImmediateRetry).
    /// </summary>
    /// <param name="entityName">Nom de l'entité Service Bus</param>
    void IncrementImmediateRetry(string entityName);

    /// <summary>
    /// Incrémente le compteur de retries exponentiels (ExponentialRetry).
    /// </summary>
    /// <param name="entityName">Nom de l'entité Service Bus</param>
    void IncrementExponentialRetry(string entityName);

    /// <summary>
    /// Définit la jauge du nombre de sessions actives.
    /// </summary>
    /// <param name="count">Nombre de sessions actives</param>
    void SetActiveSessions(long count);

    /// <summary>
    /// Définit la jauge du nombre de senders en cache.
    /// </summary>
    /// <param name="count">Nombre de senders en cache</param>
    void SetCachedSenders(long count);
}
