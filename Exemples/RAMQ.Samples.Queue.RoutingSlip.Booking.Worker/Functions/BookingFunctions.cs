using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.RoutingSlip.Booking.Message;

namespace RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Functions
{
    /// <summary>
    /// Azure Functions du Worker Routing Slip Booking.
    ///
    /// Chaque Function correspond à une Queue Service Bus = une étape du slip.
    ///
    /// Pattern IServiceScopeFactory :
    ///   - <c>IRoutingSlipExecutor</c> est enregistré comme <b>Scoped</b> par AddRoutingSlipActivity.
    ///   - Un scope explicite est créé par invocation via <c>IServiceScopeFactory</c>.
    ///   - Cela garantit la durée de vie correcte du Scoped executor, même si la classe
    ///     Functions est traitée comme Singleton par le host Azure Functions.
    ///
    /// Règles :
    ///   - AutoCompleteMessages = false obligatoire (settlement géré par le framework).
    ///   - Aucune logique métier ici — déléguer entièrement à l'activité.
    ///   - Aucun try/catch — l'IRoutingSlipExecutor gère les erreurs et le DLQ.
    /// </summary>
    public class BookingFunctions : BaseRoutingSlipFunction
    {
        private readonly ILogger<BookingFunctions> _logger;

        public BookingFunctions(
            ILogger<BookingFunctions> logger,
            IMessagingProvider messagingProvider,
            IServiceScopeFactory scopeFactory)
            : base(messagingProvider, scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ── Étape 1 : Réserver la voiture ────────────────────────────────────────

        [Function("ReserverVoiture")]
        public async Task ReserverVoiture(
            [ServiceBusTrigger("sbq-rcp-routingslipcarreservation-unit", Connection = "ServiceBusConnection",
                AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "ReserverVoiture — MessageId={Id}, DeliveryCount={Count}",
                message.MessageId, message.DeliveryCount);

            await ProcessStepAsync<BookCarArgs>("ReserverVoiture", message, actions, cancellationToken);
        }

        // ── Étape 2 : Réserver l'hôtel ───────────────────────────────────────────

        [Function("ReserverHotel")]
        public async Task ReserverHotel(
            [ServiceBusTrigger("sbq-rcp-routingsliphotelreservation-unit", Connection = "ServiceBusConnection",
                AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "ReserverHotel — MessageId={Id}, DeliveryCount={Count}",
                message.MessageId, message.DeliveryCount);

            await ProcessStepAsync<BookHotelArgs>("ReserverHotel", message, actions, cancellationToken);
        }

        // ── Étape 3 : Réserver le vol ─────────────────────────────────────────────

        [Function("ReserverVol")]
        public async Task ReserverVol(
            [ServiceBusTrigger("sbq-rcp-routingslipflightreservation-unit", Connection = "ServiceBusConnection",
                AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "ReserverVol — MessageId={Id}, DeliveryCount={Count}",
                message.MessageId, message.DeliveryCount);

            await ProcessStepAsync<BookFlightArgs>("ReserverVol", message, actions, cancellationToken);
        }
    }
}
