#pragma warning disable
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.Samples.Topic.PubSub.Consumer;
using RAMQ.Samples.Topic.PubSub.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.AIS.Sample.Topic.PubSub.Subscribers
{
    public class FlightBookingActivator
    {
        private readonly ILogger<FlightBookingActivator> _logger;
        private readonly FlightBookingConsumer _consumer;

        public FlightBookingActivator(
            ILogger<FlightBookingActivator> logger,
            FlightBookingConsumer consumer)
        {
            _logger = logger;
            _consumer = consumer;
        }

        [Function(nameof(FlightBookingActivator))]
        public async Task Run(
            [ServiceBusTrigger("notification-topic", "FlightBookingSubscription",
                               Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("FlightBookingActivator triggered MessageId={MessageId}", message.MessageId);

            try
            {
                _consumer.BindContext(message, messageActions);

                var deserResult = await _consumer.DeserializeMessageAsync<NotifyEvent>();
                if (!deserResult.IsSuccess)
                {
                    _logger.LogWarning("Désérialisation NotifyEvent échouée MessageId={MessageId} Raison={Reason} -> DeadLetter",
                        message.MessageId, deserResult.FailureReason);
                    await _consumer.DeadLetterMessageAsync(new ImmediateDLQException($"Désérialisation NotifyEvent échouée : {deserResult.ErrorMessage}"), cancellationToken);
                    return;
                }
                var ctx = deserResult.Value!;

                var result = await _consumer.ConsumeAsync(ctx, cancellationToken);

                _logger.LogInformation("FlightBookingActivator termin� MessageId={MessageId} Status={StatusCode}",
                    result.MessageId, result.Message?.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur traitement FlightBooking MessageId={MessageId}", message.MessageId);
                try { await _consumer.DeadLetterMessageAsync(ex, cancellationToken); } catch { }
                throw;
            }
        }
    }
}
