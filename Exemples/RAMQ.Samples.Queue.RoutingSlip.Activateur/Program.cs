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
using RAMQ.Samples.Queue.RoutingSlip.Message;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        var configuration = hostContext.Configuration;
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));

        // Configuration du producer — l'activateur publie des SlipEnvelope
        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(
            sp => sp.GetRequiredService<ProducerConfigurationService>());
        services.AddSingleton<IProducerConfigurationService>(
            sp => sp.GetRequiredService<ProducerConfigurationService>());

        // Producer qui publie le slip vers la première étape
        // Pas de target fixe — le target est dynamique (première étape du slip)
        services.AddProducer<SlipEnvelope>("ValiderAdmissibilite");

        // Résolveur d'endpoints — utilisé par RoutingSlipBuilder
        services.AddSingleton<IEndpointResolver, EndpointResolver>();

        // RoutingSlipBuilder — factory pour construire les slips
        services.AddTransient<RoutingSlipBuilder>(sp =>
            new RoutingSlipBuilder(
                "TraiterDossierBeneficiaire",
                sp.GetRequiredService<IEndpointResolver>()));

        // Fournisseurs Azure (Service Bus, Blob, Table)
        services.ConfigureAzureProviders(new VisualStudioCredential());
    });

builder.Build().Run();
