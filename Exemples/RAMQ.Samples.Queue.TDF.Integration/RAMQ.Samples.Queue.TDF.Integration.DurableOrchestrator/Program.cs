using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator.Options;
using RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator.Telemetry;

// ──────────────────────────────────────────────────────────────────────────────
// DFO — Composition du host StateFul
//
// Ordre d'enregistrement Application Insights (IMPORTANT) :
//   1. ITelemetryInitializer AVANT AddApplicationInsightsTelemetryWorkerService
//      → Le SDK AI collecte les ITelemetryInitializer au moment de sa propre
//        initialisation. S'il est enregistré après, l'initialiseur est ignoré.
//   2. ConfigureFunctionsApplicationInsights() adapte le pipeline AI pour
//      le modèle Azure Functions Isolated Worker (routage des ILogger vers AI).
//
// DFO — Logging classique vs Observabilité :
//   • ILogger (logging classique) : structuré par message, capturé par AI Traces.
//     Scope enrichi par enableScopeProperties dans host.json → customDimensions AI.
//   • ITelemetryInitializer (observabilité) : enrichit TOUS les types de télémétrie
//     (Requests, Dependencies, Exceptions) avec cloud.roleName, cloud.roleInstance.
//     Ces dimensions alimentent l'Application Map et les alertes multi-services.
// ──────────────────────────────────────────────────────────────────────────────

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        // ── Observabilité DFO : identité du service dans AI ───────────────────
        // Enregistré EN PREMIER — doit précéder AddApplicationInsightsTelemetryWorkerService.
        services.AddSingleton<ITelemetryInitializer, StateFulTelemetryInitializer>();

        // ── Application Insights ──────────────────────────────────────────────
        // AddApplicationInsightsTelemetryWorkerService : initialise le pipeline AI
        // (collecteur de télémétrie, channel, retry buffer vers ingestion AI).
        services.AddApplicationInsightsTelemetryWorkerService();
        // ConfigureFunctionsApplicationInsights : adapte le bridge ILogger → AI
        // pour le modèle Isolated Worker (nécessaire pour les customDimensions de scope).
        services.ConfigureFunctionsApplicationInsights();
        // AppInsights injecte opts.MinLevel = Warning ET des règles Warning.
        // ── Options DFO : seuils configurables externalisés ───────────────────
        // Lie la section "StateFul" de local.settings.json → StateFulOptions.
        // L'orchestrateur injecte IOptions<StateFulOptions> pour lire CorrelationTimeoutSeconds.
        services.Configure<StateFulOptions>(
            ctx.Configuration.GetSection(StateFulOptions.SectionName));
    })
    .Build();

await host.RunAsync();

internal sealed class ServiceBusCorrelationInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        var traceparent = Activity.Current?.GetTagItem("messaging.source.traceparent") as string;
        if (traceparent == null) return;
        var parts = traceparent.Split('-');
        if (parts.Length < 4 || parts[1].Length != 32) return;
        telemetry.Context.Operation.Id       = parts[1];
        telemetry.Context.Operation.ParentId = parts[2];
    }
}

internal sealed class AppInsightsNoiseFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry dep)
        {
            var data = dep.Data ?? string.Empty;
            var type = dep.Type ?? string.Empty;
            if (data.Contains("applicationinsights.azure.com") ||
                data.Contains("livediagnostics.monitor.azure.com") ||
                data.Contains("login.microsoftonline.com") ||
                data.Contains("FunctionRpc") ||
                data.Contains("/v2/track") ||
                data.Contains("/v2.1/track") ||
                data.Contains("/Settlement/") ||
                type.Contains("Microsoft.AAD") ||
                type.Contains("Microsoft.Tables") ||
                type.StartsWith("Azure Service Bus", StringComparison.OrdinalIgnoreCase) ||
                type.StartsWith("Azure table", StringComparison.OrdinalIgnoreCase) ||
                type == "InProc")
                return;
        }
        next.Process(item);
    }
}