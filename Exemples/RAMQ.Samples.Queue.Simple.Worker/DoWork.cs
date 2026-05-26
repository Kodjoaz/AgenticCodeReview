using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.Samples.Queue.Simple.Message;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.AIS.Sample.Queue.SimpleComplete.Sender;
    
public class DoWork
{
    private static int _counter = 0;
    private readonly ILogger<DoWork> _logger;
    private readonly IMessageProducer<SimpleMessage> _producer;

    public DoWork(ILogger<DoWork> logger, IMessageProducer<SimpleMessage> producer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    [Function("SimpleCompleteSender")]
    public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("Timer trigger executed at: {time}", DateTime.UtcNow);

        int count = Interlocked.Increment(ref _counter);
        string target = (count % 2 != 0) ? "SimpleConsumer" : "PublishConsumer";

        var context = new MessageTransitContext<SimpleMessage>
        {
            Message = new SimpleMessage
            {
                Id = Guid.NewGuid(),
                TargetConsumer = target,
                Content = $"Message for {target} (Counter: {count})"
            },
            MessageType = typeof(SimpleMessage).Name
        };

        try
        {
            var published = await _producer.PublishAsync(context);

            _logger.LogInformation(
                "Message published MessageId={MessageId} Target={Target} Counter={Counter}",
                published.MessageId,
                target,
                count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message MessageId={MessageId}", context.MessageId);
        }

        if (myTimer.ScheduleStatus is not null)
            _logger.LogInformation("Next schedule: {next}", myTimer.ScheduleStatus.Next);
    }
}