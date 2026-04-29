using System;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Represents a journal entry to record messaging events.
    /// </summary>
    public record JournalEntry(
        string Consumer,
        string Action,
        string MessageId,
        string CorrelationId,
        string Target,
        OperationMode Mode,
        int StatusCode,
        int DeliveryCount,
        int MaxDeliveryCount,
        string DeadLetterReason,
        DateTime EnqueuedTimeUtc,
        string? DeadLetterSource,
        string? SessionId,
        string? ApplicationName)
    {
        /// <summary>
        /// Factory method pour créer une entrée journal pour une publication réussie.
        /// </summary>
        public static JournalEntry ForPublish(
            string consumer,
            string action,
            string messageId,
            string correlationId,
            string target,
            string? sessionId = null,
            string? applicationName = null,
            DateTime? enqueuedTimeUtc = null)
        {
            return new JournalEntry(
                Consumer: consumer,
                Action: action,
                MessageId: messageId,
                CorrelationId: correlationId,
                Target: target,
                Mode: OperationMode.PUBLISH,
                StatusCode: 200,
                DeliveryCount: 1,
                MaxDeliveryCount: 0,
                DeadLetterReason: string.Empty,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: null,
                SessionId: sessionId,
                ApplicationName: applicationName);
        }

        /// <summary>
        /// Factory method pour créer une entrée journal pour un retry exponentiel.
        /// </summary>
        public static JournalEntry ForRetry(
            string consumer,
            string action,
            string messageId,
            string correlationId,
            string target,
            int deliveryCount,
            int maxDeliveryCount,
            string retryReason,
            string? sessionId = null,
            string? applicationName = null,
            DateTime? enqueuedTimeUtc = null)
        {
            return new JournalEntry(
                Consumer: consumer,
                Action: action,
                MessageId: messageId,
                CorrelationId: correlationId,
                Target: target,
                Mode: OperationMode.RETRY,
                StatusCode: 429, // Too Many Requests / Transient
                DeliveryCount: deliveryCount,
                MaxDeliveryCount: maxDeliveryCount,
                DeadLetterReason: retryReason,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: null,
                SessionId: sessionId,
                ApplicationName: applicationName);
        }

        /// <summary>
        /// Factory method pour créer une entrée journal pour un dead-lettering.
        /// </summary>
        public static JournalEntry ForDLQ(
            string consumer,
            string action,
            string messageId,
            string correlationId,
            string target,
            int deliveryCount,
            int maxDeliveryCount,
            string dlqReason,
            string? dlqSource = null,
            string? sessionId = null,
            string? applicationName = null,
            DateTime? enqueuedTimeUtc = null)
        {
            return new JournalEntry(
                Consumer: consumer,
                Action: action,
                MessageId: messageId,
                CorrelationId: correlationId,
                Target: target,
                Mode: OperationMode.DLQ,
                StatusCode: 410, // Gone (MaxDeliveryCount exceeded or fatal error)
                DeliveryCount: deliveryCount,
                MaxDeliveryCount: maxDeliveryCount,
                DeadLetterReason: dlqReason,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: dlqSource,
                SessionId: sessionId,
                ApplicationName: applicationName);
        }

        /// <summary>
        /// Factory method pour créer une entrée journal pour un request-reply.
        /// </summary>
        public static JournalEntry ForRequestReply(
            string consumer,
            string action,
            string messageId,
            string correlationId,
            string target,
            int statusCode,
            string? sessionId = null,
            string? applicationName = null,
            DateTime? enqueuedTimeUtc = null)
        {
            return new JournalEntry(
                Consumer: consumer,
                Action: action,
                MessageId: messageId,
                CorrelationId: correlationId,
                Target: target,
                Mode: OperationMode.REQUEST_REPLY,
                StatusCode: statusCode,
                DeliveryCount: 1,
                MaxDeliveryCount: 0,
                DeadLetterReason: string.Empty,
                EnqueuedTimeUtc: enqueuedTimeUtc ?? DateTime.UtcNow,
                DeadLetterSource: null,
                SessionId: sessionId,
                ApplicationName: applicationName);
        }
    }
}
