using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.Samples.Queue.MultiTarget.Message;

namespace RAMQ.Samples.Queue.MultiTarget.Producer
{
    /// <summary>Producer pour les messages destinés à Flight (sbq-rcp-routingslipflightreservation-unit).</summary>
    public class FlightProducer(
        IMessageProducer<FlightMessage> producer,
        ILogger<FlightProducer> logger)
        : MultiTargetProducer<FlightMessage>(producer, logger)
    {
        protected override string NomTarget => "Flight";

        protected override MessageTransitContext<FlightMessage>? CreerContexte(string target, Guid id, string content)
        {
            if (!target.Equals(NomTarget, StringComparison.OrdinalIgnoreCase))
                return null;

            return new MessageTransitContext<FlightMessage>
            {
                MessageId = id.ToString("N"),
                Message   = new FlightMessage { Id = id, Content = content }
            };
        }
    }
}
