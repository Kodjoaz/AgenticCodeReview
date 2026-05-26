namespace RAMQ.Samples.RoutingSlip.Booking.Message
{
    /// <summary>
    /// Requête HTTP reçue par l'activateur pour démarrer une réservation.
    /// Contient toutes les données nécessaires pour construire le SlipEnvelope.
    /// </summary>
    public record BookingRequest
    {
        /// <summary>Identifiant unique de la réservation (généré par l'appelant ou l'activateur).</summary>
        public Guid ReservationId { get; init; } = Guid.NewGuid();

        /// <summary>Modèle de voiture souhaité (ex: "Toyota Camry").</summary>
        public string CarModel { get; init; } = string.Empty;

        /// <summary>Nom de l'hôtel souhaité (ex: "Marriott Centre-Ville").</summary>
        public string HotelName { get; init; } = string.Empty;

        /// <summary>Préférence de chambre (ex: "Standard", "Suite").</summary>
        public string HotelRoomPreference { get; init; } = "Standard";

        /// <summary>Nom du vol souhaité (ex: "AC421 Montréal→Paris").</summary>
        public string FlightName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Arguments de l'étape 1 : Réserver la voiture.
    /// Données minimales transmises à <see cref="RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities.BookCarActivity"/>.
    /// </summary>
    public record BookCarArgs
    {
        public Guid ReservationId { get; init; }
        public string CarModel    { get; init; } = string.Empty;
    }

    /// <summary>
    /// Arguments de l'étape 2 : Réserver l'hôtel.
    /// Données minimales transmises à <see cref="RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities.BookHotelActivity"/>.
    /// </summary>
    public record BookHotelArgs
    {
        public Guid ReservationId      { get; init; }
        public string HotelName        { get; init; } = string.Empty;
        public string RoomPreference   { get; init; } = "Standard";
    }

    /// <summary>
    /// Arguments de l'étape 3 : Réserver le vol.
    /// Données minimales transmises à <see cref="RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities.BookFlightActivity"/>.
    /// </summary>
    public record BookFlightArgs
    {
        public Guid ReservationId { get; init; }
        public string FlightName  { get; init; } = string.Empty;
    }
}
