using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure;
using RAMQ.Samples.Topic.PubSub.Events;
using RAMQ.Samples.MessageTransitHelper;
using System;
using System.Threading;

namespace RAMQ.AIS.Sample.Queue.SimpleComplete.Sender;

public class DoWork
{
    private static int _counter = 0;

    private readonly ILogger<DoWork> _logger;
    private readonly IMessageProducer<NotifyEvent> _anyProducer;
   

    public DoWork(ILogger<DoWork> logger,
                  IMessageProducer<NotifyEvent> anyProducer)
    {
        _logger = logger;
        _anyProducer = anyProducer;        
    }

    [Function("PubSubWorker")]
    public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);

        string target = (_counter % 2 == 0) ? "SimpleConsumer" : "PublishConsumer";
        Interlocked.Increment(ref _counter);

        var notifyEventContext = new MessageTransitContext<NotifyEvent>
        {
            Message = new NotifyEvent
            {
                ReservationId = Guid.NewGuid(),
                Content = "Customer reservation carfailed"
            },
            MessageType = typeof(NotifyEvent).AssemblyQualifiedName,
            MessageId = Guid.NewGuid().ToString("N")
        };

        
        var properties = new Dictionary<string, object>
        {
            { AzureMessagingProperties.Consumer, "All" },
            { AzureMessagingProperties.Action, "All" }
        };

        try
        {
            var published = await _anyProducer.PublishAsync(notifyEventContext, new PublishOptions
            {
                Properties = properties
            });
            _logger.LogInformation("Message published Id={MessageId} Target={Target}",
                published.MessageId, target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while publishing message.");
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }
}
