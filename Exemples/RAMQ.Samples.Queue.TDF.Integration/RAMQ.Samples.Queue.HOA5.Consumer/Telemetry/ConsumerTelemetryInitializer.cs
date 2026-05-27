using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace RAMQ.Samples.Queue.HOA5.Consumer.Telemetry;

public sealed class ConsumerTelemetryInitializer : ITelemetryInitializer
{
    private static readonly string InstanceId =
        Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName;

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName     = "HOA5-Consumer";
        telemetry.Context.Cloud.RoleInstance = InstanceId;
        telemetry.Context.Component.Version  = "1.0.0-poc";
    }
}
