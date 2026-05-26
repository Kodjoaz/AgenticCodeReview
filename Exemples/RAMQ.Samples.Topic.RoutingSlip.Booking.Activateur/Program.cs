using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.ConfigurationService;

// ┌─────────────────────────────────────────────────────────────────────────────┐
// │  RAMQ.Samples.Topic.RoutingSlip.Activateur — Program.cs                     │
// │                                                                             │
// │  Point d'entrée HTTP qui lance le workflow Routing Slip via un TOPIC.       │
// │                                                                             │
// │  Différence par rapport à la version Queue :                                │
// │    - AppSettings.Endpoints utilise EntityType = "Topic" + Subscription.     │
// │    - Un seul topic Service Bus transporte le slip entre toutes les étapes.  │
// │    - Chaque abonnement filtre par stepName grâce aux ApplicationProperties. │
// │    - Le code de l'activateur est identique — seule la config change.        │
// │                                                                             │
// │  Schéma infrastructure Topic :                                              │
// │    POST /dossiers → [topic-dossiers]                                        │
// │                        ├─ sub-valider  (filtre: StepName = "ValiderAdmissibilite")  │
// │                        ├─ sub-enrichir (filtre: StepName = "EnrichirDonnees")       │
// │                        └─ sub-notifier (filtre: StepName = "NotifierBeneficiaire")  │
// └─────────────────────────────────────────────────────────────────────────────┘

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        // Configuration du producer
        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(
            sp => sp.GetRequiredService<ProducerConfigurationService>());
        services.AddSingleton<IProducerConfigurationService>(
            sp => sp.GetRequiredService<ProducerConfigurationService>());

        // Producer → publie le slip vers le topic (première étape = ValiderAdmissibilite)
        services.AddProducer<SlipEnvelope>("ReserverVoiture");

        // Résolveur d'endpoints — lit AppSettings.Endpoints pour construire les URLs Topic
        services.AddSingleton<IEndpointResolver, EndpointResolver>();

        // RoutingSlipBuilder — crée le slip avec les 3 étapes
        services.AddTransient<RoutingSlipBuilder>(sp =>
            new RoutingSlipBuilder(
                "BookingTopic",
                sp.GetRequiredService<IEndpointResolver>()));

        // Fournisseurs Azure
        services.ConfigureAzureProviders(new VisualStudioCredential());
    });

builder.Build().Run();
