using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace RAMQ.Samples.Queue.TDF.Integration.Subscriber.Telemetry;

public sealed class SubscriberTelemetryInitializer : ITelemetryInitializer
{
    private static readonly string InstanceId =
        Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName;

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName     = "TDF-SeqCon-Subscriber";
        telemetry.Context.Cloud.RoleInstance = InstanceId;
        telemetry.Context.Component.Version  = "1.0.0-poc";
    }
}

