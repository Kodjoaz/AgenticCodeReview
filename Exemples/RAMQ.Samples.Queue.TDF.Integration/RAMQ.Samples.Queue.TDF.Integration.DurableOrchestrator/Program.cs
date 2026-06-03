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
        // PostConfigure reset les deux → logs RAMQ.* passent via relay gRPC → host (couleurs natives func CLI).
        services.PostConfigure<LoggerFilterOptions>(opts =>
        {
            opts.MinLevel = LogLevel.None;
            opts.Rules.Add(new LoggerFilterRule(null, "RAMQ",      LogLevel.Information, null));
            opts.Rules.Add(new LoggerFilterRule(null, "Azure",     LogLevel.Warning,     null));
            opts.Rules.Add(new LoggerFilterRule(null, "Microsoft", LogLevel.Warning,     null));
            opts.Rules.Add(new LoggerFilterRule(null, "System",    LogLevel.Warning,     null));
        });

        // ── Options DFO : seuils configurables externalisés ───────────────────
        // Lie la section "StateFul" de local.settings.json → StateFulOptions.
        // L'orchestrateur injecte IOptions<StateFulOptions> pour lire CorrelationTimeoutSeconds.
        services.Configure<StateFulOptions>(
            ctx.Configuration.GetSection(StateFulOptions.SectionName));
    })
    .Build();

await host.RunAsync();






