using System.Diagnostics;

namespace RAMQ.Samples.RoutingSlip.Booking.Message;

/// <summary>
/// Source de traces OpenTelemetry pour les exemples de réservation voyage (RoutingSlip Booking).
///
/// <para>
/// À enregistrer dans le <c>TracerProvider</c> des projets Worker et Activateur :
/// </para>
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(t => t
///         .AddSource(BookingTelemetry.SourceName));
/// </code>
///
/// <para>
/// Spans métier émis par les activités :
/// </para>
/// <list type="table">
///   <item><term>booking.car.reserve</term><description>Tentative de réservation voiture (BookCarActivity).</description></item>
///   <item><term>booking.hotel.reserve</term><description>Tentative de réservation hôtel (BookHotelActivity).</description></item>
///   <item><term>booking.flight.reserve</term><description>Tentative de réservation vol (BookFlightActivity).</description></item>
///   <item><term>booking.compensate</term><description>Déclenchement de la compensation (rollback LIFO).</description></item>
/// </list>
///
/// <para>
/// Ces spans sont des <em>enfants</em> du span <c>routing_slip.step</c> émis par EMT.
/// La hiérarchie résultante dans Jaeger / Azure Monitor est :
/// </para>
/// <code>
/// [messaging.consume]
///   └─ [routing_slip.step : ReserverVoiture]
///        └─ [booking.car.reserve]            ← span métier
///   └─ [routing_slip.step : ReserverHotel]
///        └─ [booking.hotel.reserve]          ← span métier
///             └─ [booking.compensate]        ← si Fault()
///   └─ [routing_slip.step : ReserverVol]
///        └─ [booking.flight.reserve]         ← span métier
///             └─ [booking.compensate]        ← si Fault()
/// </code>
/// </summary>
public static class BookingTelemetry
{
    /// <summary>
    /// Nom de la source à enregistrer via <c>AddSource(BookingTelemetry.SourceName)</c>.
    /// Valeur : <c>"RAMQ.Samples.RoutingSlip.Booking"</c>.
    /// </summary>
    public const string SourceName = "RAMQ.Samples.RoutingSlip.Booking";

    /// <summary>
    /// ActivitySource partagée par toutes les activités de réservation.
    /// Initialisée une seule fois (singleton de processus), thread-safe.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, "1.0.0");
}
