using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.Samples.Queue.MultiTarget.Message;

namespace RAMQ.Samples.Queue.MultiTarget.Producer
{
    /// <summary>Producer pour les messages destinés à Hotel (sbq-rcp-routingsliphotelreservation-unit).</summary>
    public class HotelProducer(
        IMessageProducer<HotelMessage> producer,
        ILogger<HotelProducer> logger)
        : MultiTargetProducer<HotelMessage>(producer, logger)
    {
        protected override string NomTarget => "Hotel";

        protected override MessageTransitContext<HotelMessage>? CreerContexte(string target, Guid id, string content)
        {
            if (!target.Equals(NomTarget, StringComparison.OrdinalIgnoreCase))
                return null;

            return new MessageTransitContext<HotelMessage>
            {
                MessageId = id.ToString("N"),
                Message   = new HotelMessage { Id = id, Content = content }
            };
        }
    }
}
