using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.Samples.Queue.RequestReply.Message;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.RequestReply.Consumer
{
    /// <summary>
    /// Consumer Request/Reply : reçoit un RequestMessage et publie une réponse typée via IMessageProducer.
    /// </summary>
    public class RequestReplyConsumer : BaseConsumer<RequestMessage>
    {
        private readonly IMessageProducer<ReplyMessage> _replyProducer;

        public RequestReplyConsumer(
            IMessagingProvider messagingProvider,
            ILogger<RequestReplyConsumer> logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            IMessageProducer<ReplyMessage> replyProducer,
            string? targetName = null,
            string? consumerName = null,
            string? actionName = null)
            : base(messagingProvider, logger, config, serializer, storageProvider, targetName, consumerName, actionName)
        {
            _replyProducer = replyProducer ?? throw new ArgumentNullException(nameof(replyProducer));
        }

        public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<RequestMessage> context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            using var scope = Logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"] = context.MessageId,
                ["SessionId"] = context.SessionId,
                ["RequestId"] = context.Message?.Id
            });

            var reply = new MessageTransitResponse
            {
                StatusCode = (int)System.Net.HttpStatusCode.OK,
                Content = $"Reply for Request Id={context.Message!.Id}"
            };

            try
            {
                var replyPayload = new ReplyMessage
                {
                    Id      = context.Message!.Id,
                    Content = context.Message.Content + " - replied from consumer"
                };

                var replyContext = new MessageTransitContext<ReplyMessage>
                {
                    Message     = replyPayload,
                    MessageType = typeof(ReplyMessage).AssemblyQualifiedName,
                    MessageId   = Guid.NewGuid().ToString("N"),
                    SessionId   = context.SessionId,
                    CorrelationId = context.MessageId
                };

                var published = await _replyProducer.PublishAsync(replyContext, null, cancellationToken);
                Logger.LogInformation("Réponse envoyée MessageId={ReplyMessageId} CorrelationId={RequestId}",
                    published.MessageId, context.MessageId);

                await CompleteMessageAsync(cancellationToken);
            }
            catch (ImmediateDLQException ex)
            {
                reply.IsPermanentFailure = true;
                await DeadLetterMessageAsync(ex, cancellationToken);
            }
            catch (ImmediateRetryException ex)
            {
                reply.IsTransient = true;
                await ImmediateRetryAsync(ex, cancellationToken);
            }
            catch (ExponentialRetryException ex)
            {
                reply.IsTransient = true;
                await ExponentialRetryAsync(ex, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Annulation RequestReplyConsumer MessageId={MessageId}", context.MessageId);
                throw;
            }
            catch (Exception ex)
            {
                reply.IsTransient = true;
                Logger.LogError(ex, "Erreur RequestReplyConsumer MessageId={MessageId}", context.MessageId);
                await DeadLetterMessageAsync(ex, cancellationToken);
            }

            return context.CopyWithResponse(reply);
        }
    }
}


