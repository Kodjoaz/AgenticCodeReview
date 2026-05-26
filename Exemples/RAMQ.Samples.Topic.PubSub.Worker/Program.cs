using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Topic.PubSub.Events;
using RAMQ.AIS.Sample.Queue.SimpleComplete.Sender;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;

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

        // Configuration sections
        services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        // Services de configuration (Producer)
        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());
        services.AddSingleton<IProducerConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());
                
        // Producer (NotifyEvent) — target fixé via AddProducer
        services.AddProducer<NotifyEvent>("notifyevent");

        // Enregistrement centralisé des providers Azure
        services.ConfigureAzureProviders(new VisualStudioCredential());
    });

builder.Build().Run();


