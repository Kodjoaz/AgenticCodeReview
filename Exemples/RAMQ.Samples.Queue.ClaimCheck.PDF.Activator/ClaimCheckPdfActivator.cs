using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.Samples.Queue.ClaimCheck.PDF.Consumer;

namespace RAMQ.Samples.Queue.ClaimCheck.PDF.Activator
{
    public class ClaimCheckPdfActivator
    {
        private readonly ILogger<ClaimCheckPdfActivator> _logger;
        private readonly ClaimCheckPdfConsumer _consumer;

        public ClaimCheckPdfActivator(
            ILogger<ClaimCheckPdfActivator> logger,
            ClaimCheckPdfConsumer consumer)
        {
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
            _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
        }

        [Function(nameof(ClaimCheckPdfActivator))]
        public async Task Run(
            [ServiceBusTrigger("sbq-claimcheck-pdf",
                               Connection = "ServiceBusConnection",
                               AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(actions);

            _logger.LogInformation(
                "Réception message ClaimCheck. MessageId={MessageId} DeliveryCount={DeliveryCount}",
                message.MessageId, message.DeliveryCount);

            // Étape 1 — Lier le contexte Service Bus au consumer.
            try
            {
                _consumer.BindContext(message, actions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BindContext échoué MessageId={MessageId} → DLQ", message.MessageId);
                await actions.DeadLetterMessageAsync(message, null, "BindContextFailed", ex.Message, cancellationToken);
                return;
            }

            // Étape 2 — Désérialiser le message (gère le Claim Check automatiquement :
            // si IsClaimCheckApplied, EMT télécharge le payload depuis le Blob avant désérialisation).
            var deserResult = await _consumer.DeserializeMessageAsync<
                RAMQ.Samples.Queue.ClaimCheck.PDF.Message.PdfRapportMessage>(cancellationToken);

            if (!deserResult.IsSuccess || deserResult.Value == null)
            {
                _logger.LogWarning(
                    "Désérialisation échouée MessageId={MessageId} Raison={Reason} → DLQ",
                    message.MessageId, deserResult.FailureReason);
                await _consumer.DeadLetterMessageAsync(
                    new InvalidOperationException($"Désérialisation échouée : {deserResult.ErrorMessage}"),
                    cancellationToken);
                return;
            }

            // Étape 3 — Traiter le message (téléchargement inline ou référence selon le consumer).
            try
            {
                await _consumer.ConsumeAsync(deserResult.Value, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Traitement annulé MessageId={MessageId}", message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception non gérée MessageId={MessageId} → DLQ", message.MessageId);
                await _consumer.DeadLetterMessageAsync(ex, cancellationToken);
            }
        }
    }
}
