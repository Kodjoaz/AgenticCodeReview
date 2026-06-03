extern alias AzureIdentity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Services;
using RAMQ.Samples.RoutingSlip.Booking.Message;



var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        // Dotnet-isolated : le filtre lambda a la priorité absolue — il ne peut pas
        // être overridé par AddApplicationInsightsTelemetryWorkerService ni par
        // ConfigureFunctionsWorkerDefaults, contrairement à SetMinimumLevel/AddFilter.
        // RAMQ.* → Information ; frameworks → Warning ; reste → Information.
        logging.SetMinimumLevel(LogLevel.None); // laisser passer, le lambda décide
        logging.AddFilter((category, level) =>
        {
            if (category is null) return level >= LogLevel.Information;
            if (category.StartsWith("RAMQ"))       return level >= LogLevel.Information;
            if (category.StartsWith("Azure"))      return level >= LogLevel.Warning;
            if (category.StartsWith("Microsoft"))  return level >= LogLevel.Warning;
            if (category.StartsWith("System"))     return level >= LogLevel.Warning;
            return level >= LogLevel.Information;
        });
    })
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // AppInsights injecte un filtre Warning global APRÈS ConfigureLogging.
        // On l'override ici (après l'enregistrement AppInsights) pour garantir
        // que RAMQ.* passe à Information jusqu'au relay gRPC → host console.
        services.Configure<Microsoft.Extensions.Logging.LoggerFilterOptions>(opts =>
            opts.Rules.Add(new Microsoft.Extensions.Logging.LoggerFilterRule(
                providerName:  null,
                categoryName:  "RAMQ",
                logLevel:      Microsoft.Extensions.Logging.LogLevel.Information,
                filter:        null)));

        // ── OpenTelemetry : traces distribuées ────────────────────────────────────────
        var appInsightsConnectionString = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        var telemetryBuilder = services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.AddSource(EMTInstrumentation.SourceName);
                t.AddSource(BookingTelemetry.SourceName);
                t.AddHttpClientInstrumentation();
            })
            .WithMetrics(m =>
            {
                m.AddMeter(EMTInstrumentation.SourceName);
            })
            .UseFunctionsWorkerDefaults();

        var credential = new AzureIdentity::Azure.Identity.VisualStudioCredential();

        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
            telemetryBuilder.UseAzureMonitorExporter(o => o.Credential = credential);

        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleConsumerDefaults(ctx.Configuration, credential);

        services.AddScoped<IBookingCompensationService, BookingCompensationService>();

        services.AddRoutingSlipActivity<BookCarActivity,    BookCarArgs>  ("ReserverVoiture");
        services.AddRoutingSlipActivity<BookHotelActivity,  BookHotelArgs>("ReserverHotel");
        services.AddRoutingSlipActivity<BookFlightActivity, BookFlightArgs>("ReserverVol");


    });

builder.Build().Run();
