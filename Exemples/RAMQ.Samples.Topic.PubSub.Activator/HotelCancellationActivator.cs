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
    public class HotelCancellationActivator
    {
        private readonly ILogger<HotelCancellationActivator> _logger;
        private readonly HotelCancellationConsumer _consumer;

        public HotelCancellationActivator(
            ILogger<HotelCancellationActivator> logger,
            HotelCancellationConsumer consumer)
        {
            _logger = logger;
            _consumer = consumer;
        }

        [Function(nameof(HotelCancellationActivator))]
        public async Task Run(
            [ServiceBusTrigger("notification-topic", "HotelCancellationSubscription",
                               Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("HotelCancellationActivator triggered MessageId={MessageId}", message.MessageId);

            try
            {
                _consumer.BindContext(message, messageActions);

                var deserResult = await _consumer.DeserializeMessageAsync<NotifyEvent>(cancellationToken);
                if (!deserResult.IsSuccess)
                {
                    _logger.LogWarning("Désérialisation NotifyEvent échouée MessageId={MessageId} Raison={Reason} -> DeadLetter",
                        message.MessageId, deserResult.FailureReason);
                    await _consumer.DeadLetterMessageAsync(new ImmediateDLQException($"Désérialisation NotifyEvent échouée : {deserResult.ErrorMessage}"), cancellationToken);
                    return;
                }
                var ctx = deserResult.Value!;

                var result = await _consumer.ConsumeAsync(ctx, cancellationToken);

                _logger.LogInformation("HotelCancellationActivator termin� MessageId={MessageId} Status={StatusCode}",
                    result.MessageId, result.Message?.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur traitement HotelCancellation MessageId={MessageId}", message.MessageId);
                try { await _consumer.DeadLetterMessageAsync(ex, cancellationToken); } catch { }
                throw;
            }
        }
    }
}