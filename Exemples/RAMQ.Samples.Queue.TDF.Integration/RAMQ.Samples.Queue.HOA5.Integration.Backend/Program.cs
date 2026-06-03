using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.Samples.Queue.HOA5.Integration.Backend.Telemetry;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<ITelemetryInitializer, BackendTelemetryInitializer>();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        // AppInsights injecte opts.MinLevel = Warning ET des règles Warning.
        // PostConfigure reset les deux → logs RAMQ.* passent via relay gRPC → host (couleurs natives func CLI).
        services.PostConfigure<LoggerFilterOptions>(opts =>
        {
            opts.MinLevel = LogLevel.None;
            opts.Rules.Add(new LoggerFilterRule(null, "RAMQ",      LogLevel.Information, null));
            opts.Rules.Add(new LoggerFilterRule(null, "Azure",     LogLevel.Warning,     null));
            opts.Rules.Add(new LoggerFilterRule(null, "Microsoft", LogLevel.Warning,     null));
            opts.Rules.Add(new LoggerFilterRule(null, "System",    LogLevel.Warning,     null));
        });
    })
    .Build();

await host.RunAsync();




