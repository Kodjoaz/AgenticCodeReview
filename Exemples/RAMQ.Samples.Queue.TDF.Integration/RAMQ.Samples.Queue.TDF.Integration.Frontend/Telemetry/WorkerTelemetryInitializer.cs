using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace RAMQ.Samples.Queue.TDF.Integration.Frontend.Telemetry;

/// <summary>
/// Initialise les propriétés cloud de télémétrie Application Insights pour ce service.
/// Permet d'identifier l'instance dans les tableaux de bord AI multi-services.
/// </summary>
public sealed class FrontendTelemetryInitializer : ITelemetryInitializer
{
    private static readonly string InstanceId =
        Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName;

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName     = "TDF-Integration-Frontend";
        telemetry.Context.Cloud.RoleInstance = InstanceId;
        telemetry.Context.Component.Version  = "1.0.0-poc";
    }
}


