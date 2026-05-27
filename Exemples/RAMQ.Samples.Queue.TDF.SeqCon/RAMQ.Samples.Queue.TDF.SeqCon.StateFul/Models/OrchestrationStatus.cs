namespace RAMQ.Samples.Queue.TDF.SeqCon.StateFul.Models;

/// <summary>
/// DFO — Statut structuré de l'orchestration exposé via ctx.SetCustomStatus().
///
/// Pourquoi un statut structuré (pas une simple chaîne) ?
///   • Durable Functions Monitor peut filtrer et requêter ce JSON directement.
///   • Application Insights reçoit le statut sérialisé dans les traces de l'orchestrateur.
///   • Azure Monitor / Grafana peut déclencher des alertes sur Stage == "Failed_*".
///   • Un opérateur peut voir l'état en temps réel sans ouvrir AI :
///       GET /orchestrations/{instanceId} → CustomStatus contient ce JSON.
///
/// Règle DFO « État visible de l'extérieur » :
///   L'orchestrateur met à jour le statut à CHAQUE transition d'état, pas seulement
///   à la fin. Cela permet à l'équipe d'opérations de savoir OÙ est bloquée
///   une orchestration sans attendre sa complétion ou son échec.
///
/// DFO — Différence logging classique / observabilité sur ce type :
///   • Logging classique  : logger.LogInformation("Stage={Stage}", stage)
///     → ligne de texte dans AI Traces, corrélée par OperationId AI.
///   • Observabilité DFO  : ctx.SetCustomStatus(new OrchestrationStatus { Stage, DurationSeconds })
///     → JSON requêtable via l'API Durable Functions, visible dans les dashboards.
///     Les deux sont complémentaires : logs pour le DÉBOGAGE, statut pour le MONITORING.
/// </summary>
public sealed record OrchestrationStatus
{
    /// <summary>
    /// État actuel de la machine à états. Valeurs : voir <see cref="OrchestrationStage"/>.
    /// Utilisé pour les filtres dans Durable Functions Monitor et les alertes Azure Monitor.
    /// </summary>
    public required string   Stage          { get; init; }

    /// <summary>Clé de corrélation principale — lie tous les messages de la transaction TDF.</summary>
    public required string   SessionId      { get; init; }

    /// <summary>Identifiant métier de l'échange — visible dans les dashboards opérationnels.</summary>
    public required string   NumeroEchange  { get; init; }

    /// <summary>MessageId du message déclencheur (Étape 2).</summary>
    public required string   InitialMsgId   { get; init; }

    /// <summary>Horodatage UTC de démarrage (déterministe via ctx.CurrentUtcDateTime).</summary>
    public required DateTime StartedAt      { get; init; }

    /// <summary>
    /// DFO — Durée totale en secondes depuis le démarrage.
    /// Null tant que l'orchestration n'est pas terminée (succès ou échec).
    /// Exposé dans le statut pour permettre les alertes SLA sans requêter AI.
    /// </summary>
    public          double?  DurationSeconds { get; init; }

    /// <summary>
    /// Horodatage UTC de fin (succès ou échec).
    /// Null tant que l'orchestration est en cours.
    /// </summary>
    public          DateTime? CompletedAt   { get; init; }

    /// <summary>Code d'erreur machine — null si succès. Exemples : "TIMEOUT", "INVALID_TOKEN".</summary>
    public          string?  ErrorCode      { get; init; }

    /// <summary>Message d'erreur lisible par un opérateur.</summary>
    public          string?  ErrorMessage   { get; init; }
}

/// <summary>
/// Constantes d'état du cycle de vie de l'orchestration TDF.
/// Représentées en chaînes pour la sérialisation JSON et la requêtabilité dans Durable Monitor.
/// </summary>
public static class OrchestrationStage
{
    /// <summary>L'orchestrateur attend l'événement CorrellerEnvoyer (Étape 3).</summary>
    public const string AwaitingCorrelation = "AwaitingCorrelation";

    /// <summary>L'événement CorrellerEnvoyer a été reçu, validation en cours.</summary>
    public const string Correlating         = "Correlating";

    /// <summary>Corrélation réussie, résultat retourné au Subscriber.</summary>
    public const string Completed           = "Completed";

    /// <summary>Timeout : CorrellerEnvoyer non reçu dans le délai configuré.</summary>
    public const string Failed_Timeout      = "Failed_Timeout";

    /// <summary>Token invalide : corrélation croisée ou message malformé détecté.</summary>
    public const string Failed_Token        = "Failed_Token";
}

