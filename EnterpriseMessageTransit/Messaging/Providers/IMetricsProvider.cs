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

    // -------------------------------------------------------------------------
    // Phase 2 (P2-A3) — métriques manquantes pour l'enveloppe opérationnelle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Définit l'état courant du circuit breaker pour une entité (0=Closed, 1=Open, 2=HalfOpen).
    /// </summary>
    /// <param name="entityName">Entité Service Bus ciblée</param>
    /// <param name="state">0 = Closed · 1 = Open · 2 = HalfOpen</param>
    void SetCircuitState(string entityName, int state);

    /// <summary>
    /// Incrémente le compteur de transitions du circuit breaker.
    /// </summary>
    /// <param name="entityName">Entité Service Bus ciblée</param>
    /// <param name="from">État de départ (Closed | Open | HalfOpen)</param>
    /// <param name="to">État d'arrivée (Closed | Open | HalfOpen)</param>
    void IncrementCircuitTransition(string entityName, string from, string to);

    /// <summary>
    /// Incrémente le compteur d'échecs de désérialisation.
    /// </summary>
    /// <param name="reason">Valeur de <see cref="Serialization.DeserializationFailureReason"/> (Malformed, TooLarge, Empty…)</param>
    void IncrementDeserializationFailure(string reason);

    /// <summary>
    /// Enregistre la durée d'upload d'un blob claim-check (en millisecondes).
    /// </summary>
    /// <param name="durationMs">Durée en millisecondes</param>
    /// <param name="entityName">Entité Service Bus associée</param>
    void RecordClaimCheckUploadDuration(double durationMs, string entityName);

    /// <summary>
    /// Enregistre la durée de download d'un blob claim-check (en millisecondes).
    /// </summary>
    /// <param name="durationMs">Durée en millisecondes</param>
    /// <param name="entityName">Entité Service Bus associée</param>
    void RecordClaimCheckDownloadDuration(double durationMs, string entityName);

    /// <summary>
    /// Enregistre la durée d'écriture du journal (en millisecondes).
    /// Permet de mesurer l'impact du pattern A5 (journal hors chemin critique).
    /// </summary>
    /// <param name="durationMs">Durée en millisecondes</param>
    void RecordJournalWriteDuration(double durationMs);

    /// <summary>
    /// Incrémente le compteur de doublons détectés (message MessageId déjà traité).
    /// </summary>
    /// <param name="entityName">Entité Service Bus sur laquelle le doublon a été détecté</param>
    void IncrementDuplicateDetected(string entityName);

    /// <summary>
    /// Incrémente le compteur d'uploads claim-check réussis.
    /// </summary>
    /// <param name="entityName">Nom du fichier ou du conteneur Blob</param>
    void IncrementClaimCheckUploads(string entityName);

    /// <summary>
    /// Incrémente le compteur de downloads claim-check réussis.
    /// </summary>
    /// <param name="entityName">Nom du fichier ou du conteneur Blob</param>
    void IncrementClaimCheckDownloads(string entityName);

    /// <summary>
    /// Incrémente le compteur de compensations routing slip déclenchées (FaultResult).
    /// </summary>
    /// <param name="slipName">Nom du routing slip</param>
    /// <param name="reason">Raison de la compensation</param>
    void IncrementRoutingSlipCompensation(string slipName, string reason);
}
