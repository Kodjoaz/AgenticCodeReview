using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.Samples.Queue.MultiTarget.Message;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.MultiTarget.Consumer
{
    public class CarConsumer : BaseConsumer<CarMessage>
    {
        public CarConsumer(
            IMessagingProvider messagingProvider,
            ILogger<CarConsumer> logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            string? targetName = null,
            string? consumerName = null,
            string? actionName = null)
            : base(messagingProvider, logger, config, serializer, storageProvider, targetName, consumerName, actionName) { }

        public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<CarMessage> context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            using var scope = Logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"] = context.MessageId,
                ["SessionId"] = context.SessionId
            });

            var reply = new MessageTransitResponse
            {
                StatusCode = (int)System.Net.HttpStatusCode.OK,
                Content = "Car OK"
            };

            try
            {
                await CompleteMessageAsync(cancellationToken);
            }
            catch (ImmediateDLQException exDLQ)
            {
                // ✅ Gère directement DLQ immédiate
                reply.StatusCode = exDLQ.StatusCode ?? (int)System.Net.HttpStatusCode.InternalServerError;
                reply.Content = exDLQ.Message;
                reply.IsPermanentFailure = true;
                await DeadLetterMessageAsync(exDLQ, cancellationToken);
            }
            catch (ImmediateRetryException ex)
            {
                // ✅ Gère directement le retry immédiat
                reply.StatusCode = ex.StatusCode ?? (int)System.Net.HttpStatusCode.Conflict;
                reply.Content = ex.Message;
                reply.IsTransient = true;
                await ImmediateRetryAsync(ex, cancellationToken);
            }
            catch (ExponentialRetryException ex)
            {
                // ✅ Gère directement le retry exponentiel
                reply.StatusCode = ex.StatusCode ?? (int)System.Net.HttpStatusCode.TooManyRequests;
                reply.Content = ex.Message;
                reply.IsTransient = true;
                await ExponentialRetryAsync(ex, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Annulation en cours CarConsumer MessageId={MessageId}", context.MessageId);
                throw;
            }
            catch (Exception ex)
            {
                reply.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
                reply.Content = ex.Message;
                reply.IsTransient = true;
                Logger.LogError(ex, "Erreur CarConsumer MessageId={MessageId}", context.MessageId);
                await DeadLetterMessageAsync(ex, cancellationToken);
            }

            return context.CopyWithResponse(reply);
        }
    }
}