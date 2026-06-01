namespace RAMQ.Samples.Queue.MultiTarget.Message
{
    /// <summary>
    /// Interface marqueur commune à tous les messages de réservation.
    /// Permet d'utiliser IMultiTargetProducer&lt;IBookingMessage&gt; pour router
    /// CarMessage, HotelMessage et FlightMessage vers leurs queues respectives.
    /// </summary>
    public interface IBookingMessage { }
}
