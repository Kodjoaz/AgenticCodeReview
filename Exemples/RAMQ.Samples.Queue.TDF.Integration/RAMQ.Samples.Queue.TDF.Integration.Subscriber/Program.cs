using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refit;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.TDF.Integration.Consumer;
using RAMQ.Samples.Queue.TDF.Integration.Consumer.Http;
using RAMQ.Samples.Queue.TDF.Integration.Subscriber.Telemetry;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, cfg) =>
    {
        cfg.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<ITelemetryInitializer, SubscriberTelemetryInitializer>();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        // Client HTTP Refit vers HOA5.Backend — requis par TdfSeqConConsumer (tdf.correller)
        var hoa5Url = ctx.Configuration["Hoa5BackendUrl"] ?? "http://localhost:7072";
        services.AddRefitClient<IHoa5BackendApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(hoa5Url));

        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());

        services.AddConsumer<TdfSeqConConsumer>();

        services.ConfigureAzureProviders(new VisualStudioCredential());
    })
    .Build();

await host.RunAsync();

