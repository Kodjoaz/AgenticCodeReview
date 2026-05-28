using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.MultiTarget.Message;
using RAMQ.Samples.Queue.MultiTarget.Producer;
using RAMQ.Samples.Queue.MultiTarget.Worker;

var builder = new HostBuilder()
    .ConfigureAppConfiguration((_, config) =>
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

        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleProducerDefaults(configuration, new VisualStudioCredential());

        // Producers — target correspond à EndpointSettings.Target dans local.settings.json.
        // Chaque type de message est lié à un target distinct via IMessageTargetMap.
        services.AddProducer<CarMessage>("Car");
        services.AddProducer<HotelMessage>("Hotel");
        services.AddProducer<FlightMessage>("Flight");

        // Patron Strategy : chaque IMultiTargetProducer gère un target spécifique.
        services.AddTransient<IMultiTargetProducer, CarProducer>();
        services.AddTransient<IMultiTargetProducer, HotelProducer>();
        services.AddTransient<IMultiTargetProducer, FlightProducer>();
        services.AddTransient<MultiTargetPublicationService>();

        services.AddHostedService<DoWork>();
    });

builder.Build().Run();
