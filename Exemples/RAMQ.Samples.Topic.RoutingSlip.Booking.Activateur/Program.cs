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
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddSimpleConsole(opts => { opts.IncludeScopes = false; opts.TimestampFormat = "HH:mm:ss.fff "; });
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
        var appInsightsCs =
            ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
            ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(appInsightsCs))
        {
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();
        }
        // AppInsights injecte opts.MinLevel = Warning ET des règles Warning.
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



