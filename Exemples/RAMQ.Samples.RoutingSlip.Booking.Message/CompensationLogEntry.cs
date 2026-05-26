namespace RAMQ.Samples.RoutingSlip.Booking.Message
{
    /// <summary>
    /// Entrée dans le journal de compensation d'un RoutingSlip.
    ///
    /// <para>
    /// Chaque étape réussie ajoute une entrée dans Variables["CompensationLog"]
    /// afin de permettre aux étapes suivantes, en cas d'échec, d'annuler les
    /// opérations déjà effectuées (pattern «compensation transactionnelle»).
    /// </para>
    ///
    /// <para>
    /// Le log est exécuté en ordre <b>inverse (LIFO)</b> : la dernière étape réussie
    /// est compensée en premier, ce qui garantit la cohérence de l'annulation.
    /// </para>
    ///
    /// <para>
    /// Exemple de flux pour une réservation (Voiture → Hôtel → Vol) :
    /// <list type="number">
    ///   <item>ReserverVoiture réussit → log = [{ReserverVoiture, CAR-xxx, Voiture}]</item>
    ///   <item>ReserverHotel réussit  → log = [{ReserverVoiture, …}, {ReserverHotel, HTL-xxx, Hotel}]</item>
    ///   <item>ReserverVol échoue     → compensation : annule HTL-xxx puis CAR-xxx</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="StepName">Nom logique de l'étape (ex: "ReserverVoiture").</param>
    /// <param name="ConfirmationId">Identifiant retourné par le service externe (ex: "CAR-abc123").</param>
    /// <param name="ServiceType">Type de service à annuler : "Voiture", "Hotel" ou "Vol".</param>
    public record CompensationLogEntry(
        string StepName,
        string ConfirmationId,
        string ServiceType);
}
