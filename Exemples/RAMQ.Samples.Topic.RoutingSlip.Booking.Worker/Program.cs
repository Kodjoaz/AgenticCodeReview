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
using RAMQ.Samples.RoutingSlip.Booking.Message;
using RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Activities;
using RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Services;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.None);
        logging.AddSimpleConsole(opts =>
        {
            opts.ColorBehavior  = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
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

        services.AddRoutingSlipActivityForTopic<BookCarActivity,    BookCarArgs>  ("ReserverVoiture");
        services.AddRoutingSlipActivityForTopic<BookHotelActivity,  BookHotelArgs>("ReserverHotel");
        services.AddRoutingSlipActivityForTopic<BookFlightActivity, BookFlightArgs>("ReserverVol");
    });

builder.Build().Run();










