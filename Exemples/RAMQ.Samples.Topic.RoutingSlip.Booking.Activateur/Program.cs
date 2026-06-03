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
        // Le relay gRPC worker→host ne fonctionne pas avec Functions.Worker 2.51.0/net8.
        // AddSimpleConsole écrit directement sur stdout du worker, pipé au terminal func CLI.
        // Les filtres provider-spécifiques ne peuvent pas être overridés par AppInsights.
        logging.SetMinimumLevel(LogLevel.None);
        logging.AddSimpleConsole(opts =>
        {
            opts.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            opts.IncludeScopes  = false;
            opts.TimestampFormat = "HH:mm:ss.fff ";
        });
        logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>("RAMQ",       LogLevel.Information);
        logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>("Azure",      LogLevel.Warning);
        logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>("Microsoft",  LogLevel.Warning);
        logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>("System",     LogLevel.Warning);
    })
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();


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







