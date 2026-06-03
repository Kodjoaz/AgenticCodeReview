using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.MultiTarget.Activator;
using RAMQ.Samples.Queue.MultiTarget.Consumer;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddSimpleConsole(opts => { opts.IncludeScopes = false; opts.TimestampFormat = "HH:mm:ss.fff "; });
        logging.AddFilter("Azure",     LogLevel.Warning);
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System",    LogLevel.Warning);
    })
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        // AppInsights injecte opts.MinLevel = Warning ET des règles Warning.
        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleConsumerDefaults(hostContext.Configuration, new VisualStudioCredential());

        // Consumers multi-target — target correspond à EndpointSettings.Target dans local.settings.json.
        services.AddConsumer<CarConsumer>("Car", "BookingConsumer", "ReserverVoiture");
        services.AddConsumer<HotelConsumer>("Hotel", "BookingConsumer", "ReserverHotel");
        services.AddConsumer<FlightConsumer>("Flight", "BookingConsumer", "ReserverVol");
    });

builder.Build().Run();

