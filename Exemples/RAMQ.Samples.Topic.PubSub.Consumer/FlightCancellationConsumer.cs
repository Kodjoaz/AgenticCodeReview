using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.Samples.Topic.PubSub.Events;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Topic.PubSub.Consumer
{
    public class FlightCancellationConsumer : BaseConsumer<NotifyEvent>
    {
        public FlightCancellationConsumer(
            IMessagingProvider messagingProvider,
            ILogger<FlightCancellationConsumer> logger,
            IConsumerConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider,
            string? targetName = null,
            string? consumerName = null,
            string? actionName = null)
            : base(messagingProvider, logger, config, serializer, storageProvider, targetName, consumerName, actionName) { }

        public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
            MessageTransitContext<NotifyEvent> context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.Message, nameof(context.Message));
            using var scope = Logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"] = context.MessageId,
                ["SessionId"] = context.SessionId
            });

            var reply = new MessageTransitResponse
            {
                StatusCode = (int)System.Net.HttpStatusCode.Accepted,
                Content = $"FlightCancellation démarré (ReservationId={context.Message.ReservationId})"
            };

            try
            {
                Logger.LogInformation("FlightCancellationConsumer start MessageId={MessageId} ReservationId={ReservationId}",
                    context.MessageId, context.Message!.ReservationId);

                await Task.Delay(350, cancellationToken);

                if (!string.IsNullOrWhiteSpace(context.Message.Content) &&
                    context.Message.Content.Contains("flightfailed", StringComparison.OrdinalIgnoreCase))
                    throw new ExponentialRetryException("Échec transitoire flight cancellation", statusCode: 503);

                await CompleteMessageAsync(cancellationToken);

                reply.Content = $"FlightCancellation complété (ReservationId={context.Message.ReservationId})";
                Logger.LogInformation("FlightCancellationConsumer success MessageId={MessageId} ReservationId={ReservationId}",
                    context.MessageId, context.Message.ReservationId);
            }
            catch (ImmediateDLQException dlq)
            {
                reply.StatusCode = dlq.StatusCode ?? (int)System.Net.HttpStatusCode.InternalServerError;
                reply.Content = dlq.Message;
                reply.IsPermanentFailure = true;
                await DeadLetterMessageAsync(dlq, cancellationToken);
            }
            catch (ImmediateRetryException ire)
            {
                reply.StatusCode = ire.StatusCode ?? (int)System.Net.HttpStatusCode.RequestTimeout;
                reply.Content = ire.Message;
                reply.IsTransient = true;
                await ImmediateRetryAsync(ire, cancellationToken);
            }
            catch (ExponentialRetryException xre)
            {
                reply.StatusCode = xre.StatusCode ?? (int)System.Net.HttpStatusCode.TooManyRequests;
                reply.Content = xre.Message;
                reply.IsTransient = true;
                await ExponentialRetryAsync(xre, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Annulation en cours FlightCancellationConsumer MessageId={MessageId}", context.MessageId);
                throw;
            }
            catch (Exception ex)
            {
                reply.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
                reply.Content = $"Erreur FlightCancellationConsumer: {ex.Message}";
                reply.IsTransient = true;
                Logger.LogError(ex, "Erreur général FlightCancellationConsumer MessageId={MessageId}", context.MessageId);
                await DeadLetterMessageAsync(ex, cancellationToken);
            }

            return context.CopyWithResponse(reply);
        }
    }
}
