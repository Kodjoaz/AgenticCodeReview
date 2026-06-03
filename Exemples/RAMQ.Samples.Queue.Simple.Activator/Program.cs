using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.Simple.Consumer;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        // Dotnet-isolated : AddApplicationInsightsTelemetryWorkerService injecte un filtre Warning
        // global qui bloque les Information avant le relay gRPC → host.
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddFilter("Azure",     LogLevel.Warning);
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System",    LogLevel.Warning);
    })
    .ConfigureAppConfiguration((_, config) =>
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true))
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Override filtre Warning AppInsights pour RAMQ.*
        services.Configure<Microsoft.Extensions.Logging.LoggerFilterOptions>(opts =>
            opts.Rules.Add(new Microsoft.Extensions.Logging.LoggerFilterRule(
                providerName: null, categoryName: "RAMQ",
                logLevel: Microsoft.Extensions.Logging.LogLevel.Information,
                filter: null)));

        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleConsumerDefaults(ctx.Configuration, new VisualStudioCredential());

        // Consumers — mono-audience : target résolu automatiquement depuis le premier endpoint.
        // En multi-endpoint, utiliser AddConsumer<T>("target") pour chaque consumer.
        services.AddConsumer<AnyConsumer>();
        services.AddConsumer<SimpleConsumer>();
        services.AddConsumer<PublishConsumer>();
    });

builder.Build().Run();
