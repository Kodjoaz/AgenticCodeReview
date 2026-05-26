extern alias AzureIdentity;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.RoutingSlip.Booking.Message;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── OpenTelemetry : traces distribuées ────────────────────────────────────────
        // L'activateur publie le SlipEnvelope via EMT Producer → span messaging.send propagé.
        // La trace complète (messaging.send + routing_slip.step × 3) est visible dans Jaeger.
        //
        // Exporteurs configurés selon l'environnement :
        //   • OTLP → Jaeger en développement local (docker-compose, port 4317)
        //   • Azure Monitor → Application Insights en staging/production (détection automatique via APPLICATIONINSIGHTS_CONNECTION_STRING + host.json)
        var otlpEndpoint = ctx.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var appInsightsConnectionString = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        var telemetryBuilder = services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.AddSource(EMTInstrumentation.SourceName);
                t.AddSource(BookingTelemetry.SourceName);
                t.AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .UseFunctionsWorkerDefaults();    // Configure Azure Functions + capte Application Insights automatiquement

        // Export vers Azure Monitor (prod - seulement si APPLICATIONINSIGHTS_CONNECTION_STRING est configurée)
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            telemetryBuilder.UseAzureMonitorExporter();
        }

        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        // Configuration du producer (publie le SlipEnvelope vers la 1re étape)
        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(
            sp => sp.GetRequiredService<ProducerConfigurationService>());
        services.AddSingleton<IProducerConfigurationService>(
            sp => sp.GetRequiredService<ProducerConfigurationService>());

        // Producer qui publie le slip vers la 1re étape (ReserverVoiture)
        // IMPORTANT : le target "ReserverVoiture" doit correspondre à :
        //   1. stepName dans AddRoutingSlipActivity (Worker)
        //   2. Target dans AppSettings.Endpoints (Worker local.settings.json)
        //   3. Queue Service Bus du [ServiceBusTrigger] ReserverVoiture (Worker BookingFunctions)
        // Cette coherence garantit que le slip routing fonctionne correctement.
        services.AddProducer<SlipEnvelope>("ReserverVoiture");

        // Résolveur d'endpoints pour RoutingSlipBuilder
        services.AddSingleton<IEndpointResolver, EndpointResolver>();

        // RoutingSlipBuilder — construit un slip "Booking" avec 3 étapes
        services.AddTransient<RoutingSlipBuilder>(sp =>
            new RoutingSlipBuilder("Booking", sp.GetRequiredService<IEndpointResolver>()));

        // Enregistrement centralisé des providers Azure.
        // Remarque : contrairement à MultiTarget.Activator qui utilise new VisualStudioCredential(),
        // nous utilisons ConfigureAzureProviders() sans argument pour éviter le conflit de namespace
        // entre Azure.Core 1.54.0 et Azure.Identity 1.13.0 (due aux dépendances transitives d'OpenTelemetry).
        // ConfigureAzureProviders() utilise DefaultAzureCredential qui résout automatiquement
        // les Visual Studio credentials en développement local et Managed Identity en production.
        services.ConfigureAzureProviders(new AzureIdentity::Azure.Identity.VisualStudioCredential());
    });

builder.Build().Run();
