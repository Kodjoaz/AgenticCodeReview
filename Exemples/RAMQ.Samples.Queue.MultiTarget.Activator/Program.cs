using Azure.Data.Tables;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.MultiTarget.Activator;
using RAMQ.Samples.Queue.MultiTarget.Consumer;
using RAMQ.Samples.Queue.MultiTarget.Message;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        var configuration = hostContext.Configuration;

        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));

        // ConsumerConfigurationService � singleton car stateless et partag�
        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());

        // Consumers multi-target � target correspond � EndpointSettings.Target dans local.settings.json.
        // AddConsumer<T>(target, consumer, action) transmet les m�tadonn�es � BaseConsumer
        // pour que le journal, OTel et le retry handler disposent du bon contexte.
        services.AddConsumer<CarConsumer>("Car", "BookingConsumer", "ReserverVoiture");
        services.AddConsumer<HotelConsumer>("Hotel", "BookingConsumer", "ReserverHotel");
        services.AddConsumer<FlightConsumer>("Flight", "BookingConsumer", "ReserverVol");

        // Exemples d'autres cas d'usage disponibles :
        // services.AddConsumer<CarConsumer>();                                    // Aucun param�tre
        // services.AddConsumer<CarConsumer>("Car");                               // Target uniquement
        // services.AddConsumer<CarConsumer>("Car", "BookingConsumer");            // Target + Consumer
        // services.AddConsumerWithAction<CarConsumer>("ReserverVoiture");         // Action uniquement
        // services.AddConsumerWithTargetAndAction<CarConsumer>("Car", "ReserverVoiture");  // Target + Action

        // Enregistrement centralis� des providers Azure.
        services.ConfigureAzureProviders(new VisualStudioCredential());
    });

builder.Build().Run();

