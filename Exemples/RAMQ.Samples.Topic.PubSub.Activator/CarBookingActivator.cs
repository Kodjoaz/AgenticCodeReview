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
    public class CarBookingActivator
    {
        private readonly ILogger<CarBookingActivator> _logger;
        private readonly CarBookingConsumer _consumer;

        public CarBookingActivator(
            ILogger<CarBookingActivator> logger,
            CarBookingConsumer consumer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
        }

        [Function(nameof(CarBookingActivator))]
        public async Task Run(
            [ServiceBusTrigger(
                topicName: "notification-topic",
                subscriptionName: "CarBookingSubscription",
                Connection = "ServiceBusConnection",
                AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("CarBookingActivator triggered MessageId={MessageId}", message.MessageId);

            try
            {
                // Bind du contexte pour permettre au provider d�op�rer (Complete / DeadLetter / Retry)
                _consumer.BindContext(message, messageActions);

                // D�s�rialisation (MessageTransitContext<NotifyEvent>)
                var deserResult = await _consumer.DeserializeMessageAsync<NotifyEvent>();
                if (!deserResult.IsSuccess)
                {
                    _logger.LogWarning("Désérialisation NotifyEvent échouée MessageId={MessageId} Raison={Reason} -> DeadLetter",
                        message.MessageId, deserResult.FailureReason);
                    await _consumer.DeadLetterMessageAsync(new ImmediateDLQException($"Désérialisation NotifyEvent échouée : {deserResult.ErrorMessage}"), cancellationToken);
                    return;
                }
                var ctx = deserResult.Value!;

                // Ex�cution du consumer
                var result = await _consumer.ConsumeAsync(ctx, cancellationToken);

                _logger.LogInformation(
                    "CarBookingActivator termin� MessageId={MessageId} Status={StatusCode}",
                    result.MessageId,
                    result.Message?.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur traitement CarBooking MessageId={MessageId}", message.MessageId);
                // Tentative de DLQ
                try
                {
                    await _consumer.DeadLetterMessageAsync(ex, cancellationToken);
                }
                catch (Exception dlqEx)
                {
                    _logger.LogError(dlqEx, "DeadLetter secondaire �chou�e MessageId={MessageId}", message.MessageId);
                }
                throw; // Laisser la fonction remonter l�erreur (monitoring)
            }
        }
    }
}