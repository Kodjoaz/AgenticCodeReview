using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.MultiTarget.Message;
using RAMQ.Samples.Queue.MultiTarget.Producer;
using RAMQ.Samples.Queue.MultiTarget.Worker;

var builder = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));

        // Configuration producer — singleton car stateless et partagé
        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());
        services.AddSingleton<IProducerConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());

        // Producers — target correspond à EndpointSettings.Target dans local.settings.json.
        // Chaque type de message est lié à un target distinct via IMessageTargetMap.
        services.AddProducer<CarMessage>("Car");
        services.AddProducer<HotelMessage>("Hotel");
        services.AddProducer<FlightMessage>("Flight");

        // Patron Strategy : chaque IMultiTargetProducer gère un target spécifique.
        // L'ordre d'enregistrement définit l'ordre d'itération dans MultiTargetPublicationService.
        services.AddTransient<IMultiTargetProducer, CarProducer>();
        services.AddTransient<IMultiTargetProducer, HotelProducer>();
        services.AddTransient<IMultiTargetProducer, FlightProducer>();
        services.AddTransient<MultiTargetPublicationService>();

        // Credentials — en développement local (VisualStudioCredential), en production (ManagedIdentity).
        // DefaultAzureCredential choisit automatiquement la bonne stratégie.
        //services.AddSingleton(new DefaultAzureCredential());

        // Worker background — enregistré en tant que hosted service.
        services.AddHostedService<DoWork>();

        // Enregistrement centralisé des providers Azure.
        // VisualStudioCredential pour le développement local.
        services.ConfigureAzureProviders(new VisualStudioCredential());
    });

builder.Build().Run();





