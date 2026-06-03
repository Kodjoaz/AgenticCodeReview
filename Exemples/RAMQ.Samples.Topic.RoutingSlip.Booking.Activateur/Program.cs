using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.MessageTransitHelper;

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
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
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
        services.AddEMTSampleProducerDefaults(ctx.Configuration, new VisualStudioCredential());

        // Producer → publie le slip vers le topic (première étape = ReserverVoiture)
        services.AddProducer<SlipEnvelope>("ReserverVoiture");

        // Résolveur d'endpoints — lit AppSettings.Endpoints pour construire les URLs Topic
        services.AddSingleton<IEndpointResolver, EndpointResolver>();

        services.AddTransient<RoutingSlipBuilder>(sp =>
            new RoutingSlipBuilder("BookingTopic", sp.GetRequiredService<IEndpointResolver>()));
    });

builder.Build().Run();



