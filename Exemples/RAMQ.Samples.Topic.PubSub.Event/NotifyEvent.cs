namespace RAMQ.Samples.Topic.PubSub.Events
{
    public class NotifyEvent
    {
        public Guid ReservationId { get; init; }

        // Renommé de 'Message' à 'Content'
        public string Content { get; set; }
    }
}
