using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.Samples.Queue.MultiTarget.Consumer;
using RAMQ.Samples.Queue.MultiTarget.Message;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.MultiTarget.Activator;

public class FlightSubscriber
{
    private readonly ILogger<FlightSubscriber> _logger;
    private readonly FlightConsumer _consumer;

    public FlightSubscriber(ILogger<FlightSubscriber> logger, FlightConsumer consumer)
    {
        _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    }

    [Function(nameof(FlightSubscriber))]
    public async Task Run(
        [ServiceBusTrigger("sbq-rcp-routingslipflightreservation-unit",  // corrigé : était "target3.queue"
                           Connection = "ServiceBusConnection",
                           AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(actions);

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Annulation demandée avant traitement Id={MessageId}", message.MessageId);
            return;
        }

        _logger.LogInformation("FlightSubscriber reçu Id={MessageId}", message.MessageId);

        try
        {
            _consumer.BindContext(message, actions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BindContext échoué Id={MessageId} -> DLQ", message.MessageId);
            await actions.DeadLetterMessageAsync(message,
                deadLetterReason: ex.GetType().Name,
                deadLetterErrorDescription: ex.Message,
                cancellationToken: cancellationToken);
            return;
        }

        var deserResult = await _consumer.DeserializeMessageAsync<FlightMessage>(cancellationToken);
        if (!deserResult.IsSuccess)
        {
            _logger.LogWarning("Désérialisation échouée Id={MessageId} Raison={Reason} -> DLQ",
                message.MessageId, deserResult.FailureReason);
            await _consumer.DeadLetterMessageAsync(
                new InvalidOperationException($"Désérialisation FlightMessage échouée : {deserResult.ErrorMessage}"),
                cancellationToken);
            return;
        }

        try
        {
            var result = await _consumer.ConsumeAsync(deserResult.Value!, cancellationToken);
            _logger.LogInformation("FlightSubscriber terminé Id={MessageId} Status={StatusCode}",
                result.MessageId, result.Message?.StatusCode);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Traitement annulé Id={MessageId}", message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception non gérée Id={MessageId} -> DLQ", message.MessageId);
            await _consumer.DeadLetterMessageAsync(ex, cancellationToken);
        }
    }
}
