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
    public class CarCancellationActivator
    {
        private readonly ILogger<CarCancellationActivator> _logger;
        private readonly CarCancellationConsumer _consumer;

        public CarCancellationActivator(
            ILogger<CarCancellationActivator> logger,
            CarCancellationConsumer consumer)
        {
            _logger = logger;
            _consumer = consumer;
        }

        [Function(nameof(CarCancellationActivator))]
        public async Task Run(
            [ServiceBusTrigger("notification-topic", "CarCancellationSubscription",
                               Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("CarCancellationActivator triggered MessageId={MessageId}", message.MessageId);

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

                _logger.LogInformation("CarCancellationActivator termin� MessageId={MessageId} Status={StatusCode}",
                    result.MessageId, result.Message?.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur traitement CarCancellation MessageId={MessageId}", message.MessageId);
                try { await _consumer.DeadLetterMessageAsync(ex, cancellationToken); } catch { }
                throw;
            }
        }
    }
}