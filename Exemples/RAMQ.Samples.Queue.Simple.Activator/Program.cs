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
    .ConfigureAppConfiguration((_, config) =>
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true))
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleConsumerDefaults(ctx.Configuration, new VisualStudioCredential());

        // Consumers — mono-audience : target résolu automatiquement depuis le premier endpoint.
        // En multi-endpoint, utiliser AddConsumer<T>("target") pour chaque consumer.
        services.AddConsumer<AnyConsumer>();
        services.AddConsumer<SimpleConsumer>();
        services.AddConsumer<PublishConsumer>();
    });

builder.Build().Run();
