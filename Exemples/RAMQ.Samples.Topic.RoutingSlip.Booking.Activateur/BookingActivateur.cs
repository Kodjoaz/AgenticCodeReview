using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.RoutingSlip.Booking.Message;
using System.Net;
using System.Text.Json;

namespace RAMQ.Samples.Topic.RoutingSlip.Booking.Activateur
{
    /// <summary>
    /// Azure Function HTTP — Point d'entrée du workflow de réservation Routing Slip via Topic.
    ///
    /// <para>
    /// Expose les mêmes surfaces HTTP que la version Queue.
    /// La différence réside uniquement dans la configuration (Endpoints → EntityType = "Topic")
    /// et dans le Worker qui utilise <c>AddRoutingSlipActivityForTopic</c>.
    /// Le code de l'activateur est <b>identique</b> — le transport est transparent.
    /// </para>
    ///
    /// <list type="bullet">
    ///   <item><b>POST /bookings</b> — Endpoint générique JSON.</item>
    ///   <item><b>GET  /bookings/scenarios</b> — Liste les scénarios disponibles.</item>
    ///   <item>
    ///     <b>POST /bookings/scenarios/{scenario}</b> — Lance un scénario prédéfini :
    ///     <list type="table">
    ///       <item><term>succes-complet</term><description>Les 3 étapes réussissent → booking confirmé.</description></item>
    ///       <item><term>echec-vol</term><description>Compensation LIFO : hôtel annulé puis voiture annulée.</description></item>
    ///       <item><term>echec-hotel</term><description>Compensation : voiture annulée.</description></item>
    ///       <item><term>echec-voiture</term><description>Fault direct, rien à compenser.</description></item>
    ///     </list>
    ///   </item>
    /// </list>
    /// </summary>
    public class BookingActivateur
    {
        private readonly ILogger<BookingActivateur> _logger;
        private readonly RoutingSlipBuilder _builder;
        private readonly IMessageProducer<SlipEnvelope> _producer;

        private static readonly IReadOnlyDictionary<string, ScenarioDefinition> Scenarios =
            new Dictionary<string, ScenarioDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["succes-complet"] = new(
                    Description: "Les 3 étapes réussissent → booking confirmé, aucune compensation.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "Toyota Camry",
                        HotelName           = "Marriott Centre-Ville",
                        HotelRoomPreference = "Standard",
                        FlightName          = "AC421 Montréal→Paris"
                    }),

                ["echec-vol"] = new(
                    Description: "Voiture + hôtel réservés, vol annulé → compensation LIFO : hôtel annulé puis voiture annulée.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "Honda Civic",
                        HotelName           = "Sheraton Vieux-Montréal",
                        HotelRoomPreference = "Suite",
                        FlightName          = "ANNULE-RR990 Montréal→Rome"
                    }),

                ["echec-hotel"] = new(
                    Description: "Voiture réservée, hôtel complet → compensation : voiture annulée.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "Ford Explorer",
                        HotelName           = "COMPLET-Hilton Laval",
                        HotelRoomPreference = "Standard",
                        FlightName          = "PC101 Québec→Toronto"
                    }),

                ["echec-voiture"] = new(
                    Description: "Voiture indisponible dès l'étape 1 → Fault direct, rien à compenser.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "INDISPO-Ferrari",
                        HotelName           = "Delta Montréal",
                        HotelRoomPreference = "Standard",
                        FlightName          = "WS432 Montréal→Vancouver"
                    }),

                ["retry-transitoire-voiture"] = new(
                    Description: "Panne transitoire du service voiture → RetryExponential les 2 premiers essais (ctx.Attempt 1 et 2), succès au 3e.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "TRANSIENT-Nissan Rogue",
                        HotelName           = "Marriott Centre-Ville",
                        HotelRoomPreference = "Standard",
                        FlightName          = "AC421 Montréal→Paris"
                    }),

                ["retry-transitoire-hotel"] = new(
                    Description: "Panne transitoire du service hôtel → RetryExponential les 2 premiers essais, succès au 3e.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "Hyundai Tucson",
                        HotelName           = "TRANSIENT-Fairmont Le Reine Elizabeth",
                        HotelRoomPreference = "Deluxe",
                        FlightName          = "WJ456 Montréal→Miami"
                    }),

                ["retry-immediat-vol"] = new(
                    Description: "API vol saturée (HTTP 429) → RetryImmediate au 1er essai, succès au 2e.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "Toyota RAV4",
                        HotelName           = "Delta Montréal-Laval",
                        HotelRoomPreference = "Standard",
                        FlightName          = "THROTTLE-PC101 Montréal→Toronto"
                    }),

                ["echec-compensation"] = new(
                    Description: "Vol annulé + échec de l'annulation hôtel (API indisponible) → compensation partielle, log Critical, voiture quand même annulée (best-effort).",
                    Payload: new BookingRequest
                    {
                        CarModel            = "Honda CR-V",
                        HotelName           = "COMPFAIL-Fairmont Le Château Frontenac",
                        HotelRoomPreference = "Standard",
                        FlightName          = "ANNULE-AC888 Québec→New York"
                    }),

                ["retry-epuise"] = new(
                    Description: "Service voiture en panne permanente → RetryExponential à chaque tentative (sans limite dans l'activité). "
                               + "Après MaxDeliveryCount essais, Service Bus envoie le message en Dead Letter Queue — sans intervention du code.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "CRASH-Lamborghini Urus",
                        HotelName           = "Marriott Centre-Ville",
                        HotelRoomPreference = "Standard",
                        FlightName          = "AC421 Montréal→Paris"
                    }),

                ["court-circuit-vip"] = new(
                    Description: "Package VIP pré-confirmé → BookCarActivity retourne Complete() dès l'étape 1. "
                               + "Hôtel et vol ne sont JAMAIS appelés. Démontre Complete() comme arrêt anticipé non-erreur.",
                    Payload: new BookingRequest
                    {
                        CarModel            = "VIP-Mercedes S-Class",
                        HotelName           = "Ritz-Carlton Montréal",
                        HotelRoomPreference = "Suite Présidentielle",
                        FlightName          = "AC001 Montréal→Paris (Première Classe)"
                    })
            };

        public BookingActivateur(
            ILogger<BookingActivateur> logger,
            RoutingSlipBuilder builder,
            IMessageProducer<SlipEnvelope> producer)
        {
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
            _builder  = builder  ?? throw new ArgumentNullException(nameof(builder));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        // ── Endpoint générique ────────────────────────────────────────────────────

        [Function(nameof(BookingActivateur))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "bookings")] HttpRequestData req,
            CancellationToken cancellationToken)
        {
            BookingRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<BookingRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Corps de la requête invalide.");
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Corps JSON invalide.", cancellationToken);
                return bad;
            }

            if (request is null || string.IsNullOrWhiteSpace(request.CarModel))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("CarModel est requis.", cancellationToken);
                return bad;
            }

            return await PublierSlipAsync(req, request, cancellationToken);
        }

        // ── Endpoints de scénarios ────────────────────────────────────────────────

        [Function("ListerScenarios")]
        public async Task<HttpResponseData> ListerScenarios(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "bookings/scenarios")] HttpRequestData req,
            CancellationToken cancellationToken)
        {
            var liste = Scenarios.Select(kvp => new
            {
                Nom         = kvp.Key,
                Description = kvp.Value.Description,
                Payload     = kvp.Value.Payload
            });

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(liste, cancellationToken);
            return ok;
        }

        [Function("DemarrerScenario")]
        public async Task<HttpResponseData> DemarrerScenario(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "bookings/scenarios/{scenario}")] HttpRequestData req,
            string scenario,
            CancellationToken cancellationToken)
        {
            if (!Scenarios.TryGetValue(scenario, out var definition))
            {
                _logger.LogWarning("Scénario inconnu demandé : {Scenario}", scenario);
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new
                {
                    Erreur               = $"Scénario '{scenario}' inconnu.",
                    ScenariosDisponibles = Scenarios.Keys.Order().ToArray()
                }, cancellationToken);
                return notFound;
            }

            _logger.LogInformation(
                "Démarrage du scénario '{Scenario}' (Topic) — {Description}",
                scenario, definition.Description);

            return await PublierSlipAsync(req, definition.Payload, cancellationToken);
        }

        // ── Méthode partagée ──────────────────────────────────────────────────────

        /// <summary>
        /// Publie le slip initial avec injection transparente des propriétés de routage (Consumer/Action)
        /// dans les Application Properties du message, pour un routage Service Bus correct dès la première publication.
        /// Cette méthode applique le même principe d'encapsulation que le worker (RoutingSlipExecutor),
        /// garantissant une architecture entreprise, traçable et maintenable.
        /// </summary>
        private async Task<HttpResponseData> PublierSlipAsync(
            HttpRequestData req,
            BookingRequest request,
            CancellationToken cancellationToken)
        {
            var reservationId = request.ReservationId == Guid.Empty
                ? Guid.NewGuid()
                : request.ReservationId;

            var slip = _builder
                .AddStep("ReserverVoiture", new BookCarArgs
                {
                    ReservationId = reservationId,
                    CarModel      = request.CarModel
                })
                .AddStep("ReserverHotel", new BookHotelArgs
                {
                    ReservationId  = reservationId,
                    HotelName      = request.HotelName,
                    RoomPreference = request.HotelRoomPreference
                })
                .AddStep("ReserverVol", new BookFlightArgs
                {
                    ReservationId = reservationId,
                    FlightName    = request.FlightName
                })
                .Build();

            var context = new MessageTransitContext<SlipEnvelope>
            {
                MessageId     = slip.Header.SlipId,
                CorrelationId = slip.Header.SlipId,
                Message       = slip
            };

            // --- Injection transparente des propriétés de routage (Consumer/Action) ---
            // Alignement avec le worker : on extrait les propriétés de la première étape du slip
            // et on les injecte dans les Application Properties du message publié.

            var initialProperties = BuildInitialProperties(slip);
            var publishOptions = initialProperties != null && initialProperties.Count > 0
                ? new PublishOptions { Properties = initialProperties }
                : null;

            await _producer.PublishAsync(context, publishOptions, cancellationToken);

            _logger.LogInformation(
                "Booking slip {SlipId} publié (Topic) — ReservationId={ReservationId}, Voiture={Car}, Hôtel={Hotel}, Vol={Flight}",
                slip.Header.SlipId, reservationId, request.CarModel, request.HotelName, request.FlightName);

            var ok = req.CreateResponse(HttpStatusCode.Accepted);
            await ok.WriteAsJsonAsync(new
            {
                SlipId        = slip.Header.SlipId,
                ReservationId = reservationId,
                Etapes        = new[] { "ReserverVoiture", "ReserverHotel", "ReserverVol" }
            }, cancellationToken);
            return ok;
        }

        /// <summary>
        /// Extrait les propriétés de routage (Consumer/Action) de la première étape du slip,
        /// pour injection dans les Application Properties du message initial.
        /// Cette méthode centralise la logique d'alignement avec le RoutingSlipExecutor (worker).
        /// </summary>
        private static Dictionary<string, object> BuildInitialProperties(SlipEnvelope slip)
        {
            var step = slip.Steps[0];
            if (step.Subscription == null)
                throw new InvalidOperationException("La première étape du slip ne contient pas de Subscription (Consumer/Action non configuré).");

            if (string.IsNullOrWhiteSpace(step.Subscription.Consumer))
                throw new InvalidOperationException("La propriété 'Consumer' n'est pas configurée dans la Subscription de la première étape du slip.");

            if (string.IsNullOrWhiteSpace(step.Subscription.Action))
                throw new InvalidOperationException("La propriété 'Action' n'est pas configurée dans la Subscription de la première étape du slip.");

            var props = new Dictionary<string, object>
            {
                [MessagePropertyKeys.Consumer] = step.Subscription.Consumer!,
                [MessagePropertyKeys.Action] = step.Subscription.Action!
            };
            return props;
        }

        // ── Types internes ────────────────────────────────────────────────────────

        private record ScenarioDefinition(string Description, BookingRequest Payload);
    }
}
