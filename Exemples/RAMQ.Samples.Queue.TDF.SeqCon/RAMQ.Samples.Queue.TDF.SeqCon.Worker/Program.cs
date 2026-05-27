using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.TDF.SeqCon.Consumer.Messages;
using RAMQ.Samples.Queue.TDF.SeqCon.Worker.Options;
using RAMQ.Samples.Queue.TDF.SeqCon.Worker.Telemetry;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, cfg) =>
    {
        cfg.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<ITelemetryInitializer, WorkerTelemetryInitializer>();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<TdfActivateurOptions>(ctx.Configuration.GetSection(TdfActivateurOptions.SectionName));
        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        // BlobServiceClient — Azurite en local (loopback), credentials Azure en production
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BlobStorageSetting>>().Value;
            var uri = new Uri(settings.BlobServiceUri);
            return uri.IsLoopback
                ? new BlobServiceClient("UseDevelopmentStorage=true")
                : new BlobServiceClient(uri, new VisualStudioCredential());
        });

        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());
        services.AddSingleton<IProducerConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());

        services.AddProducer<TdfTransactionCommand>();

        services.ConfigureAzureProviders(new VisualStudioCredential());
    })
    .Build();

await host.RunAsync();
