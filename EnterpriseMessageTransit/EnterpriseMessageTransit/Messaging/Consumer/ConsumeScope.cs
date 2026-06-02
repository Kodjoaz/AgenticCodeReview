using System.Diagnostics;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer
{
    internal sealed class ConsumeScope : IDisposable
    {
        private readonly Activity? _consumeActivity;
        private readonly Activity? _deserializeActivity;

        internal ConsumeScope(Activity? consumeActivity, Activity? deserializeActivity)
        {
            _consumeActivity    = consumeActivity;
            _deserializeActivity = deserializeActivity;
        }

        internal void MarkFailure(string failureReason, string? errorMessage)
        {
            _deserializeActivity?.SetTag("deserialization.failure_reason", failureReason);
            _deserializeActivity?.SetStatus(ActivityStatusCode.Error, errorMessage ?? failureReason);
            _consumeActivity?.SetStatus(ActivityStatusCode.Error, failureReason);
        }

        internal void MarkSuccess<TMsg>(
            MessageTransitContext<TMsg> ctx,
            string entityName,
            string? consumerName,
            string? actionName) where TMsg : class
        {
            _consumeActivity?.SetTag("messaging.correlation_id", ctx.CorrelationId);
            _consumeActivity?.SetTag("messaging.consumer",       consumerName ?? string.Empty);
            _consumeActivity?.SetTag("messaging.action",         actionName   ?? string.Empty);
            _consumeActivity?.SetTag("messaging.destination",    entityName);
            _consumeActivity?.SetTag("messaging.status_code",    "200");
            _consumeActivity?.SetTag("messaging.mode",           OperationMode.COMPLETE.ToString());

            _deserializeActivity?.SetTag("messaging.message_id",  ctx.MessageId);
            _deserializeActivity?.SetTag("messaging.session_id",  ctx.SessionId);
            _deserializeActivity?.SetTag("messaging.destination", entityName);
            _deserializeActivity?.SetTag("messaging.claimcheck",  ctx.IsClaimCheckApplied ? "true" : "false");
            _deserializeActivity?.SetStatus(ActivityStatusCode.Ok);
            _consumeActivity?.SetStatus(ActivityStatusCode.Ok);
        }

        public void Dispose()
        {
            _deserializeActivity?.Dispose();
            _consumeActivity?.Dispose();
        }
    }
}
