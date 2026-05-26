using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.Samples.Queue.Simple.Message;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RAMQ.Samples.Queue.Simple.Consumer
{
    public class SimpleConsumer : BaseConsumer<SimpleMessage>
    {
        public SimpleConsumer(
            IMessagingProvider messagingProvider,
            ILogger<SimpleConsumer> logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            string? targetName = null,
            string? consumerName = null,
            string? actionName = null,
            IMetricsProvider? metricsProvider = null)
            : base(messagingProvider, logger, config, serializer, storageProvider, targetName, consumerName, actionName, metricsProvider)
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
                // Exemple: simulation d'une DLQ directe
                //throw new ImmediateDLQException("fake");
                //throw new ImmediateRetryException("fake immediate retry");
                //throw new ExponentialRetryException("fake exponential retry");
                await CompleteMessageAsync(cancellationToken);
            }
            catch (ImmediateDLQException exDLQ)
            {
                reply.StatusCode = exDLQ.StatusCode ?? (int)System.Net.HttpStatusCode.InternalServerError;
                reply.Content = exDLQ.Message;
                reply.IsPermanentFailure = true;
                await DeadLetterMessageAsync(exDLQ, cancellationToken);
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
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Annulation en cours SimpleConsumer MessageId={MessageId}", context.MessageId);
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

            return context.CopyWithResponse(reply);
        }
    }
}
