namespace RAMQ.Samples.Queue.TDF.Integration.StateFul.Options;

/// <summary>
/// DFO — Options du service StateFul externalisées depuis la configuration.
///
/// Principe DFO « Pas de valeurs magiques » :
///   Tout seuil opérationnel (timeout, retry, concurrence) doit être une valeur
///   de configuration modifiable sans recompilation ni redéploiement.
///   → Observable via Azure App Configuration.
///   → Modifiable via un feature flag sans incident de déploiement.
///   → Documenté ici avec sa valeur par défaut, son unité et sa justification.
///
/// Utilisation :
///   services.Configure&lt;StateFulOptions&gt;(config.GetSection(StateFulOptions.SectionName));
///   puis injection : IOptions&lt;StateFulOptions&gt; options
/// </summary>
public sealed class StateFulOptions
{
    /// <summary>Nom de la section dans local.settings.json / appsettings.json.</summary>
    public const string SectionName = "StateFul";

    /// <summary>
    /// Durée maximale (secondes) d'attente de l'événement CorrellerEnvoyer (Étape 3).
    ///
    /// Valeur par défaut : 30s (PoC).
    /// Production recommandée : 300–900s selon le SLA de traitement HOA5.
    ///
    /// Si ce seuil est dépassé, l'orchestration passe en Failed_Timeout :
    ///   • Une alerte Application Insights se déclenche (exception tracée).
    ///   • Le message Étape 2 n'est pas complété (retry automatique Service Bus).
    ///   • L'audit de compensation est enregistré via RecordAuditActivity.
    /// </summary>
    public int CorrelationTimeoutSeconds { get; set; } = 30;
}

