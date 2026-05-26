using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.Samples.Queue.Simple.Message;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.Simple.Consumer
{
    public class PublishConsumer : BaseConsumer<SimpleMessage>
    {
        public PublishConsumer(
            IMessagingProvider messagingProvider,
            ILogger<PublishConsumer> logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider)
            : base(messagingProvider, logger, config, serializer, storageProvider)
        {
        }

        public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<SimpleMessage> context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            using var scope = Logger.BeginScope(new Dictionary<string, object?> { ["MessageId"] = context.MessageId, ["SessionId"] = context.SessionId });

            var reply = new MessageTransitResponse { StatusCode = (int)System.Net.HttpStatusCode.OK, Content = "OK" };

            try
            {
                Logger.LogInformation("Traitement du message {MessageId}", context.MessageId);
                await CompleteMessageAsync(cancellationToken);
            }
            catch (ImmediateRetryException exImmediateRetry)
            {
                reply.StatusCode = exImmediateRetry.StatusCode ?? (int)System.Net.HttpStatusCode.Conflict;
                reply.Content = exImmediateRetry.Message;
                reply.IsTransient = true;
                await ImmediateRetryAsync(exImmediateRetry, cancellationToken);
            }
            catch (ExponentialRetryException exExponential)
            {
                reply.StatusCode = exExponential.StatusCode ?? (int)System.Net.HttpStatusCode.TooManyRequests;
                reply.Content = exExponential.Message;
                reply.IsTransient = true;
                await ExponentialRetryAsync(exExponential, cancellationToken);
            }
            catch (ImmediateDLQException exDLQ)
            {
                reply.StatusCode = exDLQ.StatusCode ?? (int)System.Net.HttpStatusCode.InternalServerError;
                reply.Content = exDLQ.Message;
                reply.IsPermanentFailure = true;
                await DeadLetterMessageAsync(exDLQ, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Annulation en cours PublishConsumer MessageId={MessageId}", context.MessageId);
                throw;
            }
            catch (Exception ex)
            {
                reply.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
                reply.Content = ex.Message;
                reply.IsTransient = true;
                Logger.LogError(ex, "Erreur générale pour MessageId={MessageId}", context.MessageId);
                await DeadLetterMessageAsync(ex, cancellationToken);
            }

            return BuildResponseContext(context, reply);
        }

        private MessageTransitContext<MessageTransitResponse> BuildResponseContext(MessageTransitContext<SimpleMessage> source, MessageTransitResponse reply) =>
            new()
            {
                MessageId = source.MessageId,
                SessionId = source.SessionId,
                SequenceNumber = source.SequenceNumber,
                Message = reply
            };
    }
}