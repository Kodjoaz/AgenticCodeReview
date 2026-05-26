using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.Samples.Queue.MultiTarget.Message;

namespace RAMQ.Samples.Queue.MultiTarget.Producer
{
    /// <summary>Producer pour les messages destinés à Car (sbq-rcp-routingslipcarreservation-unit).</summary>
    public class CarProducer(
        IMessageProducer<CarMessage> producer,
        ILogger<CarProducer> logger)
        : MultiTargetProducer<CarMessage>(producer, logger)
    {
        protected override string NomTarget => "Car";

        protected override MessageTransitContext<CarMessage>? CreerContexte(string target, Guid id, string content)
        {
            if (!target.Equals(NomTarget, StringComparison.OrdinalIgnoreCase))
                return null;

            return new MessageTransitContext<CarMessage>
            {
                MessageId = id.ToString("N"),
                Message   = new CarMessage { Id = id, Content = content }
            };
        }
    }
}
