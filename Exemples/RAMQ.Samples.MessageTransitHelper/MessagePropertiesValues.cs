namespace RAMQ.Samples.MessageTransitHelper
{
    public static class ReservationServiceTargetValues
    {
        public const string Car = "Car";
        public const string Hotel = "Hotel";
        public const string Flight = "Flight";
        public const string Reservation = "Reservation";
        public const string All = "All";
    }

    public static class ReservationServiceConsumerValues
    {
        public const string CarConsumer = "CarConsumer";
        public const string HotelConsumer = "HotelConsumer";
        public const string FlightConsumer = "FlightConsumer";
        public const string ReservationConsumer = "ReservationConsumer";
        public const string All = "All";
    }

    public static class ReservationServiceActionValues
    {
        public const string CarBooking = "CarBooking";
        public const string CarCancellation = "CarCancellation";
        public const string HotelBooking = "HotelBooking";
        public const string HotelCancellation = "HotelCancellation";
        public const string FlightBooking = "FlightBooking";
        public const string FlightCancellation = "FlightCancellation";
        public const string All = "All";
    }

    public static class GeneralTargetValues
    {
        public const string Basic = "Basic";
        public const string ClaimCheck = "ClaimCheck";
        public const string SequentialConvoy = "SequentialConvoy";
        public const string RequestReply = "RequestReply";
        public const string Target1 = "Target1";
        public const string Target2 = "Target2";
        public const string Target3 = "Target3";
        public const string Target4 = "Target4";

    }


}
