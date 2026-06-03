using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.MultiTarget.Message;
using RAMQ.Samples.Queue.MultiTarget.Worker;

var builder = new HostBuilder()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);        logging.AddSimpleConsole(opts =>
        {
            opts.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            opts.IncludeScopes  = false;
            opts.TimestampFormat = "HH:mm:ss.fff ";
        });
    })
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleProducerDefaults(configuration, new VisualStudioCredential());

        // R17 — IMultiTargetProducer<IBookingMessage> : 3 lignes au lieu de ~40 lignes de boilerplate.
        // AddTarget<T> appelle AddProducer<T>(target) en interne — aucun enregistrement supplémentaire.
        services.AddMultiTargetProducer<IBookingMessage>(b => b
            .AddTarget<CarMessage>("Car")
            .AddTarget<HotelMessage>("Hotel")
            .AddTarget<FlightMessage>("Flight"));

        services.AddHostedService<DoWork>();
    });

builder.Build().Run();




