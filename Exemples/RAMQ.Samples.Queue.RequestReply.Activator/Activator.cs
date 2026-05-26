using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.Samples.Queue.RequestReply.Consumer;
using RAMQ.Samples.Queue.RequestReply.Message;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.RequestReply.Activator;

public class Activator
{
    private readonly ILogger<Activator> _logger;
    private readonly RequestReplyConsumer _consumer;

    public Activator(ILogger<Activator> logger, RequestReplyConsumer consumer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    }

    [Function(nameof(Activator))]
    public async Task Run(
        [ServiceBusTrigger("simple.consumercomplete-queue", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("RequestReply Activator recu Id={MessageId}", message.MessageId);

        try
        {
            _consumer.BindContext(message, actions);
            _logger.LogDebug("Deserialisation RequestMessage Id={MessageId}", message.MessageId);

            var deserResult = await _consumer.DeserializeMessageAsync<RequestMessage>(cancellationToken);
            if (!deserResult.IsSuccess)
            {
                _logger.LogWarning("Deserialisation RequestMessage echouee ({Reason}) Id={MessageId} -> DLQ",
                    deserResult.FailureReason, message.MessageId);
                await _consumer.DeadLetterMessageAsync(
                    deserResult.Exception ?? new ImmediateDLQException($"Deserialisation echouee : {deserResult.FailureReason}", (int)System.Net.HttpStatusCode.BadRequest),
                    cancellationToken);
                return;
            }

            var ctx = deserResult.Value!;
            if (ctx?.Message == null)
            {
                _logger.LogWarning("Message null apres deserialisation Id={MessageId} -> DLQ", message.MessageId);
                await _consumer.DeadLetterMessageAsync(new ImmediateDLQException("Message null", (int)System.Net.HttpStatusCode.BadRequest), cancellationToken);
                return;
            }

            var result = await _consumer.ConsumeAsync(ctx, cancellationToken);

            _logger.LogInformation("RequestReply Activator termine Id={MessageId} Status={StatusCode}",
                result.MessageId,
                result.Message?.StatusCode);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Traitement annule Id={MessageId}", message.MessageId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur non geree Id={MessageId}", message.MessageId);
            try { await _consumer.DeadLetterMessageAsync(ex, cancellationToken); }
            catch (Exception dlqEx) { _logger.LogError(dlqEx, "DeadLetter secondaire echouee Id={MessageId}", message.MessageId); }
            throw;
        }
    }
}