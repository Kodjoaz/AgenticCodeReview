using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace RAMQ.Samples.Queue.TDF.Integration.Producer.Telemetry;

public sealed class ProducerTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.GlobalProperties["ServiceName"] = "TDF.Integration.Producer";
        telemetry.Context.GlobalProperties["Environment"] = "Development";
    }
}
