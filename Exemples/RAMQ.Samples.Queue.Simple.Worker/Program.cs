using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.Simple.Message;

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

        services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        // ProducerConfigurationService — singleton car stateless et partagé
        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());
        services.AddSingleton<IProducerConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());

        // Producer Scoped
        services.AddScoped<IMessageProducer<SimpleMessage>, Producer<SimpleMessage>>();

        // Enregistrement centralisé des providers Azure.
        // VisualStudioCredential pour le développement local.
        services.ConfigureAzureProviders(new VisualStudioCredential());
    });

builder.Build().Run();
