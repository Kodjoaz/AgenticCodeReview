using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace RAMQ.Samples.Queue.TDF.Integration.StateFul.Telemetry;

/// <summary>
/// DFO — Initialiseur de télémétrie Application Insights pour le service StateFul.
///
/// Problème résolu :
///   Sans cet initialiseur, toutes les Azure Functions (Worker, Subscriber, StateFul,
///   HOA5.Consumer, HOA5.Backend) apparaissent sous le même nœud générique "WebApp"
///   dans l'Application Map d'Application Insights.
///   → Impossible d'identifier quel service a produit une erreur dans la carte de topologie.
///   → Impossible de créer des alertes filtrées par service.
///   → Impossible de mesurer la latence par service dans les dashboards.
///
/// Ce que cet initialiseur apporte :
///   1. cloud.roleName        — Identifie le SERVICE dans l'Application Map AI.
///                              Chaque service TDF doit avoir son propre nom unique.
///   2. cloud.roleInstance    — Identifie l'INSTANCE (pod, VM, container).
///                              Critique pour le débogage multi-instance sous charge.
///   3. service.version       — Traçabilité des DÉPLOIEMENTS dans les dashboards.
///                              Permet de corréler un incident avec une version spécifique.
///
/// Enregistrement dans Program.cs :
///   services.AddSingleton&lt;ITelemetryInitializer, StateFulTelemetryInitializer&gt;();
///   (doit être enregistré AVANT AddApplicationInsightsTelemetryWorkerService)
///
/// DFO — Logging classique vs Observabilité :
///   • Logging classique    : texte de log structuré par ligne — pour les développeurs
///                            et le débogage d'incidents. Corrélé par SessionId / MessageId.
///   • Observabilité (DFO)  : métriques, traces distribuées, topologie de service.
///                            Cet initialiseur opère au niveau de la TÉLÉMÉTRIE, pas des logs.
///                            Il enrichit les Requests, Dependencies, Exceptions et Traces
///                            d'AI avec des dimensions permettant l'agrégation et les alertes.
/// </summary>
public sealed class StateFulTelemetryInitializer : ITelemetryInitializer
{
    // DFO — Le nom du service dans l'Application Map doit être stable, unique,
    // et correspondre à la nomenclature des services dans votre catalogue de services.
    private const string ServiceName    = "TDF-SeqCon-StateFul";
    private const string ServiceVersion = "1.0.0-poc";

    private readonly string _instanceId;

    public StateFulTelemetryInitializer()
    {
        // WEBSITE_INSTANCE_ID : disponible dans Azure App Service / Azure Functions.
        // En local : utilise le nom de machine pour le débogage.
        // En AKS / Container Apps : remplacer par la variable d'environnement du pod name.
        _instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")
                   ?? Environment.MachineName;
    }

    /// <summary>
    /// Appelé automatiquement par le SDK AI pour chaque Request, Dependency,
    /// Exception, Trace, Event et Metric avant l'envoi au collecteur.
    /// </summary>
    public void Initialize(ITelemetry telemetry)
    {
        // Ne pas écraser si déjà défini (ex. : injection depuis l'environnement Azure).
        if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            telemetry.Context.Cloud.RoleName = ServiceName;

        if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleInstance))
            telemetry.Context.Cloud.RoleInstance = _instanceId;

        // service.version : dimension OTel standard, utile dans les dashboards Grafana / AI Workbooks.
        if (!telemetry.Context.GlobalProperties.ContainsKey("service.version"))
            telemetry.Context.GlobalProperties["service.version"] = ServiceVersion;
    }
}

