using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.TDF.Integration.Producer.Services;
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Options;
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Services;
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Telemetry;
using Refit;

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

        // Producer HTTP client for clean abstraction
        var producerBaseUrl = ctx.Configuration["Producer:BaseUrl"] ?? "http://localhost:7071";
        services.AddRefitClient<ITdfProducerHttpClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(producerBaseUrl))
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

        // Clean abstraction for EMT protocol via Producer
        services.AddSingleton<ITdfProducerOrchestration, TdfProducerOrchestration>();
    })
    .Build();

await host.RunAsync();

