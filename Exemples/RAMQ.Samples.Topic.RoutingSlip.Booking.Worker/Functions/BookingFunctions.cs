using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.RoutingSlip.Booking.Message;

namespace RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Functions
{
    /// <summary>
    /// Azure Functions du Worker Routing Slip Booking — variante Topic.
    ///
    /// Différences par rapport à la version Queue :
    ///   - Les triggers ServiceBus utilisent topicName + subscriptionName.
    ///   - Le filtre SQL sur chaque abonnement garantit qu'un message n'est
    ///     traité que par l'activité correspondante (filtrage par stepName).
    ///   - On appelle <c>executor.ExecuteAsync</c> (Topic) au lieu de <c>ProcessAsync</c> (Queue).
    ///
    /// Pattern IServiceScopeFactory :
    ///   - <c>IRoutingSlipExecutor</c> est enregistré comme <b>Scoped</b> par AddRoutingSlipActivityForTopic.
    ///   - Un scope explicite est créé par invocation pour garantir la durée de vie correcte.
    ///
    /// Règles :
    ///   - AutoCompleteMessages = false obligatoire.
    ///   - Aucune logique métier ici.
    ///   - Aucun try/catch — l'IRoutingSlipExecutor gère les erreurs.
    /// </summary>
    public class BookingFunctions : BaseRoutingSlipFunction
    {
        private readonly ILogger<BookingFunctions> _logger;

        public BookingFunctions(
            ILogger<BookingFunctions> logger,
            IMessagingProvider messagingProvider,
            IServiceScopeFactory scopeFactory)
            : base(logger, messagingProvider, scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ── Étape 1 : Réserver la voiture (Topic → abonnement voiture) ───────────

        [Function("ReserverVoiture")]
        public async Task ReserverVoiture(
            [ServiceBusTrigger(
                topicName:        "sbt-rcp-routingslipreservation-unit",
                subscriptionName: "sbts-RCP-RoutingSlipReservationAbonmCar",
                Connection        = "ServiceBusConnection",
                AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "ReserverVoiture (Topic) — MessageId={Id}, DeliveryCount={Count}",
                message.MessageId, message.DeliveryCount);

            await ExecuteStepAsync<BookCarArgs>("ReserverVoiture", message, actions, cancellationToken);
        }

        // ── Étape 2 : Réserver l'hôtel (Topic → abonnement hôtel) ────────────────

        [Function("ReserverHotel")]
        public async Task ReserverHotel(
            [ServiceBusTrigger(
                topicName:        "sbt-rcp-routingslipreservation-unit",
                subscriptionName: "sbts-RCP-RoutingSlipReservationAbonmHotel",
                Connection        = "ServiceBusConnection",
                AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "ReserverHotel (Topic) — MessageId={Id}, DeliveryCount={Count}",
                message.MessageId, message.DeliveryCount);

            await ExecuteStepAsync<BookHotelArgs>("ReserverHotel", message, actions, cancellationToken);
        }

        // ── Étape 3 : Réserver le vol (Topic → abonnement vol) ───────────────────

        [Function("ReserverVol")]
        public async Task ReserverVol(
            [ServiceBusTrigger(
                topicName:        "sbt-rcp-routingslipreservation-unit",
                subscriptionName: "sbts-RCP-RoutingSlipReservationAbonmFlight",
                Connection        = "ServiceBusConnection",
                AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "ReserverVol (Topic) — MessageId={Id}, DeliveryCount={Count}",
                message.MessageId, message.DeliveryCount);

            await ExecuteStepAsync<BookFlightArgs>("ReserverVol", message, actions, cancellationToken);
        }
    }
}
