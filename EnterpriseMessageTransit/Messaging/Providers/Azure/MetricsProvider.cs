namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure;

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

/// <summary>
/// Implémentation concrète des métriques custom via System.Diagnostics.Metrics.
/// Expose des compteurs, histogrammes et jauges pour la monitoring avec OpenTelemetry/Application Insights.
/// </summary>
[ExcludeFromCodeCoverage]
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

    // Phase 2 (P2-A3) — métriques manquantes
    private readonly Dictionary<string, long> _circuitStates = new();
    private readonly Counter<long> _circuitTransitionsCounter = s_meter.CreateCounter<long>(
        "circuit_transitions_total",
        description: "Total number of circuit breaker state transitions");

    private readonly Counter<long> _deserializationFailuresCounter = s_meter.CreateCounter<long>(
        "deserialization_failures_total",
        description: "Total number of deserialization failures by reason");

    private readonly Histogram<double> _claimCheckUploadHistogram = s_meter.CreateHistogram<double>(
        "claim_check_upload_duration_ms",
        description: "Duration of claim-check blob upload operations in milliseconds");

    private readonly Histogram<double> _claimCheckDownloadHistogram = s_meter.CreateHistogram<double>(
        "claim_check_download_duration_ms",
        description: "Duration of claim-check blob download operations in milliseconds");

    private readonly Histogram<double> _journalWriteHistogram = s_meter.CreateHistogram<double>(
        "journal_write_duration_ms",
        description: "Duration of Message Transit Journal write operations in milliseconds");

    private readonly Counter<long> _duplicateDetectedCounter = s_meter.CreateCounter<long>(
        "duplicate_detected_total",
        description: "Total number of duplicate messages detected");

    private readonly Counter<long> _claimCheckUploadsCounter = s_meter.CreateCounter<long>(
        "claimcheck_uploads_total",
        description: "Total number of successful claim-check blob uploads");

    private readonly Counter<long> _claimCheckDownloadsCounter = s_meter.CreateCounter<long>(
        "claimcheck_downloads_total",
        description: "Total number of successful claim-check blob downloads");

    private readonly Counter<long> _routingSlipCompensationCounter = s_meter.CreateCounter<long>(
        "routing_slip_compensation_total",
        description: "Total number of routing slip compensations triggered (FaultResult)");

    private readonly ObservableGauge<long> _circuitStateGauge;

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

        // Phase 2 (P2-A3) — jauge multi-entités pour l'état du circuit breaker
        _circuitStateGauge = s_meter.CreateObservableGauge(
            "circuit_state",
            GetCircuitStates,
            description: "Current circuit breaker state per entity (0=Closed, 1=Open, 2=HalfOpen)");
    }

    private IEnumerable<Measurement<long>> GetCircuitStates()
    {
        lock (_circuitStates)
        {
            foreach (var kv in _circuitStates)
            {
                yield return new Measurement<long>(kv.Value,
                    new KeyValuePair<string, object?>("entity", kv.Key));
            }
        }
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

    // -------------------------------------------------------------------------
    // Phase 2 (P2-A3) — implémentation des métriques manquantes
    // -------------------------------------------------------------------------

    public void SetCircuitState(string entityName, int state)
    {
        lock (_circuitStates)
        {
            _circuitStates[entityName] = state;
        }
    }

    public void IncrementCircuitTransition(string entityName, string from, string to)
    {
        _circuitTransitionsCounter.Add(1,
            new KeyValuePair<string, object?>("entity", entityName),
            new KeyValuePair<string, object?>("from",   from),
            new KeyValuePair<string, object?>("to",     to));
    }

    public void IncrementDeserializationFailure(string reason)
    {
        _deserializationFailuresCounter.Add(1,
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordClaimCheckUploadDuration(double durationMs, string entityName)
    {
        _claimCheckUploadHistogram.Record(durationMs,
            new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void RecordClaimCheckDownloadDuration(double durationMs, string entityName)
    {
        _claimCheckDownloadHistogram.Record(durationMs,
            new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void RecordJournalWriteDuration(double durationMs)
    {
        _journalWriteHistogram.Record(durationMs);
    }

    public void IncrementDuplicateDetected(string entityName)
    {
        _duplicateDetectedCounter.Add(1,
            new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void IncrementClaimCheckUploads(string entityName)
    {
        _claimCheckUploadsCounter.Add(1,
            new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void IncrementClaimCheckDownloads(string entityName)
    {
        _claimCheckDownloadsCounter.Add(1,
            new KeyValuePair<string, object?>("entity_name", entityName));
    }

    public void IncrementRoutingSlipCompensation(string slipName, string reason)
    {
        _routingSlipCompensationCounter.Add(1,
            new KeyValuePair<string, object?>("slip_name", slipName),
            new KeyValuePair<string, object?>("reason",    reason));
    }

    /// <summary>
    /// Retourne le Meter singleton pour intégration avec OpenTelemetry MeterProvider.
    /// </summary>
    /// <returns>Instance Meter partagée</returns>
    public static Meter GetMeter() => s_meter;
}
