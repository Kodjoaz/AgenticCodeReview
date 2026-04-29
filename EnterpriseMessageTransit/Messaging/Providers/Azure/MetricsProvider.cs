namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure;

using System.Diagnostics.Metrics;

/// <summary>
/// Implémentation concrète des métriques custom via System.Diagnostics.Metrics.
/// Expose des compteurs, histogrammes et jauges pour la monitoring avec OpenTelemetry/Application Insights.
/// </summary>
public class MetricsProvider : IMetricsProvider
{
    private static readonly Meter s_meter = new("RAMQ.COM.EnterpriseMessageTransit", "1.0.0");

    private readonly Counter<long> _messagesSentCounter = s_meter.CreateCounter<long>(
        "messages_sent_total",
        description: "Total number of messages sent successfully");

    private readonly Counter<long> _messagesReceivedCounter = s_meter.CreateCounter<long>(
        "messages_received_total",
        description: "Total number of messages received successfully");

    private readonly Counter<long> _messagesDLQCounter = s_meter.CreateCounter<long>(
        "messages_dlq_total",
        description: "Total number of messages sent to Dead Letter Queue");

    private readonly Histogram<double> _sendDurationHistogram = s_meter.CreateHistogram<double>(
        "send_duration_ms",
        description: "Duration of message send operations in milliseconds");

    private readonly Histogram<double> _receiveDurationHistogram = s_meter.CreateHistogram<double>(
        "receive_duration_ms",
        description: "Duration of message receive operations in milliseconds");

    private readonly Histogram<double> _retryDelayHistogram = s_meter.CreateHistogram<double>(
        "retry_delay_ms",
        description: "Delay applied during exponential retry in milliseconds");

    private readonly Counter<long> _immediateRetryCounter = s_meter.CreateCounter<long>(
        "immediate_retry_total",
        description: "Total number of immediate retries");

    private readonly Counter<long> _exponentialRetryCounter = s_meter.CreateCounter<long>(
        "exponential_retry_total",
        description: "Total number of exponential retries");

    private readonly ObservableGauge<long> _activeSessions;
    private long _activeSessioCount = 0;

    private readonly ObservableGauge<long> _cachedSenders;
    private long _cachedSendersCount = 0;

    /// <summary>
    /// Initialise le fournisseur de métriques avec les jauges observables.
    /// </summary>
    public MetricsProvider()
    {
        _activeSessions = s_meter.CreateObservableGauge(
            "active_sessions",
            () => _activeSessioCount,
            description: "Current number of active Service Bus sessions");

        _cachedSenders = s_meter.CreateObservableGauge(
            "cached_senders",
            () => _cachedSendersCount,
            description: "Current number of cached ServiceBusSender instances");
    }

    public void IncrementMessagesSent(string entityName, string entityType)
    {
        _messagesSentCounter.Add(1, new KeyValuePair<string, object?>("entity_name", entityName),
                                    new KeyValuePair<string, object?>("entity_type", entityType));
    }

    public void IncrementMessagesReceived(string entityName, string entityType)
    {
        _messagesReceivedCounter.Add(1, new KeyValuePair<string, object?>("entity_name", entityName),
                                        new KeyValuePair<string, object?>("entity_type", entityType));
    }

    public void IncrementMessagesDLQ(string entityName, string reason)
    {
        _messagesDLQCounter.Add(1, new KeyValuePair<string, object?>("entity_name", entityName),
                                   new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordSendDuration(double durationMs, string entityName)
    {
        _sendDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void RecordReceiveDuration(double durationMs, string entityName)
    {
        _receiveDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void RecordRetryDelay(double delayMs, int attempt)
    {
        _retryDelayHistogram.Record(delayMs, new KeyValuePair<string, object?>("attempt", attempt));
    }

    public void IncrementImmediateRetry(string entityName)
    {
        _immediateRetryCounter.Add(1, new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void IncrementExponentialRetry(string entityName)
    {
        _exponentialRetryCounter.Add(1, new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void SetActiveSessions(long count)
    {
        _activeSessioCount = count;
    }

    public void SetCachedSenders(long count)
    {
        _cachedSendersCount = count;
    }

    /// <summary>
    /// Retourne le Meter singleton pour intégration avec OpenTelemetry MeterProvider.
    /// </summary>
    /// <returns>Instance Meter partagée</returns>
    public static Meter GetMeter() => s_meter;
}
