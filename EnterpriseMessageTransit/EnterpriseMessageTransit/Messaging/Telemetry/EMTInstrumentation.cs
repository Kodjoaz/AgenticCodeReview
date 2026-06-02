namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;

/// <summary>
/// Constantes publiques pour l'intégration OpenTelemetry avec EnterpriseMessageTransit.
///
/// <para>
/// Permet aux hôtes (Azure Functions, BackgroundService) d'enregistrer la source de traces
/// EMT dans leur <c>TracerProvider</c> sans avoir à connaître la valeur exacte du nom.
/// </para>
///
/// <para>Usage dans Program.cs :</para>
/// <code>
/// using OpenTelemetry.Trace;
/// using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
///
/// services.AddOpenTelemetry()
///     .WithTracing(t => t
///         .AddSource(EMTInstrumentation.SourceName));
/// </code>
///
/// <para>Spans émis par EMT :</para>
/// <list type="table">
///   <item><term>messaging.send</term><description>Chaque publication de message (Producer).</description></item>
///   <item><term>messaging.consume</term><description>Chaque réception de message (BaseConsumer).</description></item>
///   <item><term>routing_slip.step</term><description>Chaque exécution d'activité RoutingSlip (RoutingSlipExecutor).</description></item>
/// </list>
///
/// <para>
/// La propagation W3C Trace Context (<c>traceparent</c> / <c>tracestate</c>) est automatique :
/// le Producer injecte l'en-tête dans les propriétés applicatives Service Bus,
/// et le BaseConsumer restaure le contexte parent à la réception.
/// </para>
/// </summary>
public static class EMTInstrumentation
{
    /// <summary>
    /// Nom de la source de traces à enregistrer via <c>AddSource(EMTInstrumentation.SourceName)</c>.
    /// Valeur : <c>"RAMQ.COM.EnterpriseMessageTransit"</c>.
    /// </summary>
    public const string SourceName = "RAMQ.COM.EnterpriseMessageTransit";
}
