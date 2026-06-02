using System.Diagnostics;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;

/// <summary>
/// Source de traces distribuées OpenTelemetry pour EnterpriseMessageTransit.
///
/// Usage côté hôte (Azure Functions ou BackgroundService) :
/// <code>
/// // Program.cs
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t => t.AddSource(MessagingActivitySource.Name));
/// </code>
///
/// Chaque opération clé (publish, send, retry, DLQ, claim-check, saga) émet un span.
/// Le span parent est propagé automatiquement via Activity.Current.
/// </summary>
internal static class MessagingActivitySource
{
    private const string SourceName = "RAMQ.COM.EnterpriseMessageTransit";

    internal static readonly ActivitySource Source = new(SourceName, "1.0.0");

    /// <summary>
    /// Nom de la source à enregistrer côté hôte via <c>AddSource(MessagingActivitySource.Name)</c>.
    /// </summary>
    public static string Name => SourceName;
}
