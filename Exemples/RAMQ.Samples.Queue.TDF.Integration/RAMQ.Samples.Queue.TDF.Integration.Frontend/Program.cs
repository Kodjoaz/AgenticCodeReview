using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.TDF.Integration.Producer.Services;
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Options;
using RAMQ.Samples.Queue.TDF.Integration.Frontend.Services;
using Refit;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BlobStorageSetting>>().Value;
            var uri = new Uri(settings.BlobServiceUri);
            return uri.IsLoopback
                ? new BlobServiceClient("UseDevelopmentStorage=true")
                : new BlobServiceClient(uri, new VisualStudioCredential());
        });

        var producerBaseUrl = ctx.Configuration["Producer:BaseUrl"] ?? "http://localhost:7071";
        services.AddRefitClient<ITdfProducerHttpClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(producerBaseUrl))
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton<IProducerMessage, TDFProducerMessage>();
        services.AddHostedService<TdfBackgroundService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

await host.RunAsync();

