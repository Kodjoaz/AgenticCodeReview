using System.Diagnostics;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Entrée du Message Transit Journal (MTJ) — Business Activity Monitoring stratégique.
    /// Chaque événement de messaging (publish, retry, DLQ, step saga, compensation)
    /// produit une entrée persistée en Azure Table Storage avec rétention 7 ans (CAI).
    /// </summary>
    public record JournalEntry(
        string   Consumer,
        string   Action,
        string   MessageId,
        string   CorrelationId,
        string   Target,
        OperationMode Mode,
        int      StatusCode,
        int      DeliveryCount,
        int      MaxDeliveryCount,
        string   DeadLetterReason,
        DateTime EnqueuedTimeUtc,
        string?  DeadLetterSource,
        string?  SessionId,
        string?  ApplicationName)
    {
        // ─── Traçabilité Design For Operation (R16) ──────────────────────────────
        /// <summary>W3C TraceId de l'Activity ambiante — lien vers Application Insights.</summary>
        public string? TraceId      { get; init; }
        /// <summary>W3C SpanId de l'Activity ambiante.</summary>
        public string? SpanId       { get; init; }
        /// <summary>W3C ParentSpanId — utile pour relier le step au span parent de la saga.</summary>
        public string? ParentSpanId { get; init; }

        // ─── Routing Slip (R16) ──────────────────────────────────────────────────
        /// <summary>Identifiant unique du slip — relie toutes les entrées d'une saga.</summary>
        public string? SlipId       { get; init; }
        /// <summary>Nom logique du workflow (ex. "TraiterDossier").</summary>
        public string? SlipName     { get; init; }
        /// <summary>Index (curseur) de l'étape dans le slip.</summary>
        public int?    StepIndex    { get; init; }
        /// <summary>Nom de l'étape franchie.</summary>
        public string? StepName     { get; init; }
        /// <summary>Statut de l'étape : Completed, Faulted ou Compensated.</summary>
        public SlipStepStatus? StepStatus { get; init; }

        // ─── Factory methods — messages standard ─────────────────────────────────

        /// <summary>Entrée pour une publication réussie.</summary>
        public static JournalEntry ForPublish(
            string consumer, string action, string messageId, string correlationId,
            string target, string? sessionId = null, string? applicationName = null,
            DateTime? enqueuedTimeUtc = null)
            => new JournalEntry(
                Consumer: consumer, Action: action,
                MessageId: messageId, CorrelationId: correlationId, Target: target,
                Mode: OperationMode.PUBLISH, StatusCode: 200,
                DeliveryCount: 1, MaxDeliveryCount: 0, DeadLetterReason: string.Empty,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: null, SessionId: sessionId, ApplicationName: applicationName)
            { TraceId = GetCurrentTraceId(), SpanId = GetCurrentSpanId(), ParentSpanId = GetCurrentParentSpanId() };

        /// <summary>Entrée pour un retry exponentiel.</summary>
        public static JournalEntry ForRetry(
            string consumer, string action, string messageId, string correlationId,
            string target, int deliveryCount, int maxDeliveryCount, string retryReason,
            string? sessionId = null, string? applicationName = null, DateTime? enqueuedTimeUtc = null)
            => new JournalEntry(
                Consumer: consumer, Action: action,
                MessageId: messageId, CorrelationId: correlationId, Target: target,
                Mode: OperationMode.RETRY, StatusCode: 429,
                DeliveryCount: deliveryCount, MaxDeliveryCount: maxDeliveryCount, DeadLetterReason: retryReason,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: null, SessionId: sessionId, ApplicationName: applicationName)
            { TraceId = GetCurrentTraceId(), SpanId = GetCurrentSpanId(), ParentSpanId = GetCurrentParentSpanId() };

        /// <summary>Entrée pour un dead-lettering.</summary>
        public static JournalEntry ForDLQ(
            string consumer, string action, string messageId, string correlationId,
            string target, int deliveryCount, int maxDeliveryCount, string dlqReason,
            string? dlqSource = null, string? sessionId = null, string? applicationName = null,
            DateTime? enqueuedTimeUtc = null)
            => new JournalEntry(
                Consumer: consumer, Action: action,
                MessageId: messageId, CorrelationId: correlationId, Target: target,
                Mode: OperationMode.DLQ, StatusCode: 410,
                DeliveryCount: deliveryCount, MaxDeliveryCount: maxDeliveryCount, DeadLetterReason: dlqReason,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: dlqSource, SessionId: sessionId, ApplicationName: applicationName)
            { TraceId = GetCurrentTraceId(), SpanId = GetCurrentSpanId(), ParentSpanId = GetCurrentParentSpanId() };

        /// <summary>Entrée pour un request-reply.</summary>
        public static JournalEntry ForRequestReply(
            string consumer, string action, string messageId, string correlationId,
            string target, int statusCode, string? sessionId = null,
            string? applicationName = null, DateTime? enqueuedTimeUtc = null)
            => new JournalEntry(
                Consumer: consumer, Action: action,
                MessageId: messageId, CorrelationId: correlationId, Target: target,
                Mode: OperationMode.REQUEST_REPLY, StatusCode: statusCode,
                DeliveryCount: 1, MaxDeliveryCount: 0, DeadLetterReason: string.Empty,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: null, SessionId: sessionId, ApplicationName: applicationName)
            { TraceId = GetCurrentTraceId(), SpanId = GetCurrentSpanId(), ParentSpanId = GetCurrentParentSpanId() };

        // ─── Factory methods — Routing Slip (R16) ────────────────────────────────

        /// <summary>
        /// Entrée pour une étape de Routing Slip (Completed, Faulted).
        /// Chaque étape franchie par <c>RoutingSlipExecutor</c> produit une entrée.
        /// </summary>
        public static JournalEntry ForSlipStep(
            string slipId, string slipName, int stepIndex, string stepName,
            SlipStepStatus stepStatus, string target, string correlationId,
            string? applicationName = null, string? deadLetterReason = null,
            DateTime? enqueuedTimeUtc = null)
            => new JournalEntry(
                Consumer: slipName, Action: stepName,
                MessageId: slipId, CorrelationId: correlationId, Target: target,
                Mode: stepStatus == SlipStepStatus.Faulted ? OperationMode.DLQ : OperationMode.PUBLISH,
                StatusCode: stepStatus == SlipStepStatus.Faulted ? 500 : 200,
                DeliveryCount: 1, MaxDeliveryCount: 0,
                DeadLetterReason: deadLetterReason ?? string.Empty,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: null, SessionId: null, ApplicationName: applicationName)
            {
                SlipId     = slipId,
                SlipName   = slipName,
                StepIndex  = stepIndex,
                StepName   = stepName,
                StepStatus = stepStatus,
                TraceId    = GetCurrentTraceId(),
                SpanId     = GetCurrentSpanId(),
                ParentSpanId = GetCurrentParentSpanId()
            };

        /// <summary>
        /// Entrée pour une compensation LIFO déclenchée après un <c>Fault</c>.
        /// </summary>
        public static JournalEntry ForSlipCompensation(
            string slipId, string slipName, int stepIndex, string stepName,
            string target, string correlationId, string? compensationReason = null,
            string? applicationName = null, DateTime? enqueuedTimeUtc = null)
            => new JournalEntry(
                Consumer: slipName, Action: $"Compensate:{stepName}",
                MessageId: slipId, CorrelationId: correlationId, Target: target,
                Mode: OperationMode.DLQ, StatusCode: 200,
                DeliveryCount: 1, MaxDeliveryCount: 0,
                DeadLetterReason: compensationReason ?? string.Empty,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: "Compensation", SessionId: null, ApplicationName: applicationName)
            {
                SlipId     = slipId,
                SlipName   = slipName,
                StepIndex  = stepIndex,
                StepName   = stepName,
                StepStatus = SlipStepStatus.Compensated,
                TraceId    = GetCurrentTraceId(),
                SpanId     = GetCurrentSpanId(),
                ParentSpanId = GetCurrentParentSpanId()
            };

        /// <summary>
        /// Entrée pour la complétion réussie du dernier step du slip.
        /// </summary>
        public static JournalEntry ForSlipComplete(
            string slipId, string slipName, int totalSteps, string target,
            string correlationId, string? applicationName = null,
            DateTime? enqueuedTimeUtc = null)
            => new JournalEntry(
                Consumer: slipName, Action: "SlipComplete",
                MessageId: slipId, CorrelationId: correlationId, Target: target,
                Mode: OperationMode.PUBLISH, StatusCode: 200,
                DeliveryCount: 1, MaxDeliveryCount: 0, DeadLetterReason: string.Empty,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: null, SessionId: null, ApplicationName: applicationName)
            {
                SlipId     = slipId,
                SlipName   = slipName,
                StepIndex  = totalSteps - 1,
                StepName   = "SlipComplete",
                StepStatus = SlipStepStatus.Completed,
                TraceId    = GetCurrentTraceId(),
                SpanId     = GetCurrentSpanId(),
                ParentSpanId = GetCurrentParentSpanId()
            };

        // ─── Helpers — Activity.Current ─────────────────────────────────────────

        private static string? GetCurrentTraceId()
            => Activity.Current?.TraceId.ToString();

        private static string? GetCurrentSpanId()
            => Activity.Current?.SpanId.ToString();

        private static string? GetCurrentParentSpanId()
            => Activity.Current?.ParentSpanId.ToString();
    }
}
