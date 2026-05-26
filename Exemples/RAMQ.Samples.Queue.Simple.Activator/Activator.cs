using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.Samples.Queue.Simple.Consumer;
using RAMQ.Samples.Queue.Simple.Message;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.Simple.Activator
{
    public class Activator
    {
        private readonly ILogger<Activator> _logger;
        private readonly SimpleConsumer _simpleConsumer;
        private readonly PublishConsumer _publishConsumer;
        private readonly AnyConsumer _anyConsumer;

        public Activator(
            ILogger<Activator> logger,
            SimpleConsumer simpleConsumer,
            PublishConsumer publishConsumer,
            AnyConsumer anyConsumer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _simpleConsumer = simpleConsumer ?? throw new ArgumentNullException(nameof(simpleConsumer));
            _publishConsumer = publishConsumer ?? throw new ArgumentNullException(nameof(publishConsumer));
            _anyConsumer = anyConsumer ?? throw new ArgumentNullException(nameof(anyConsumer));
        }

        [Function(nameof(Activator))]
        public async Task Run(
            [ServiceBusTrigger("sbq-RCP-FileSession-unit",
                               Connection = "ServiceBusConnection",
                               AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions actions,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(actions);

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Annulation demandée avant traitement Id={MessageId}", message.MessageId);
                return;
            }

            _logger.LogInformation("Processing Id={MessageId}", message.MessageId);

            // Étape 1 — Désérialisation via AnyConsumer (lecture seule du message entrant).
            // BindContext peut échouer si le message ou les actions sont incompatibles avec l'adapter :
            // on DLQ immédiatement via les actions brutes car aucun consumer n'est encore lié.
            try
            {
                _anyConsumer.BindContext(message, actions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BindContext (AnyConsumer) échoué Id={MessageId} -> DLQ", message.MessageId);
                await _anyConsumer.DeadLetterMessageAsync( ex, cancellationToken);
                return;
            }

            var deserResult = await _anyConsumer.DeserializeMessageAsync<SimpleMessage>(cancellationToken);
            if (!deserResult.IsSuccess)
            {
                _logger.LogWarning("Désérialisation échouée Id={MessageId} Raison={Reason} -> DLQ",
                    message.MessageId, deserResult.FailureReason);
                await _anyConsumer.DeadLetterMessageAsync(
                    new InvalidOperationException($"Désérialisation échouée : {deserResult.ErrorMessage}"),
                    cancellationToken);
                return;
            }

            // Étape 2 — Routage et traitement métier.
            var context = deserResult.Value!;
            var targetConsumer = context.Message?.TargetConsumer;

            try
            {
                switch (targetConsumer)
                {
                    case "SimpleConsumer":
                        _simpleConsumer.BindContext(message, actions);
                        await _simpleConsumer.ConsumeAsync(context, cancellationToken);
                        break;

                    case "PublishConsumer":
                        _publishConsumer.BindContext(message, actions);
                        await _publishConsumer.ConsumeAsync(context, cancellationToken);
                        break;

                    default:
                        _logger.LogWarning("TargetConsumer inconnu '{Target}' Id={MessageId} -> DLQ",
                            targetConsumer, message.MessageId);
                        await _anyConsumer.DeadLetterMessageAsync(
                            new InvalidOperationException($"TargetConsumer inconnu : '{targetConsumer}'"),
                            cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Traitement annulé Id={MessageId}", message.MessageId);
                // Le lock expirera naturellement — ASB replacera le message dans la file.
            }
            catch (Exception ex)
            {
                // ConsumeAsync gère normalement ses propres exceptions (Complete/Retry/DLQ).
                // Ce catch ne devrait pas être atteint dans le chemin nominal.
                // On DLQ ici pour éviter un retry infini par expiration du lock.
                _logger.LogError(ex, "Exception non gérée lors du traitement Id={MessageId} -> DLQ", message.MessageId);
                await _anyConsumer.DeadLetterMessageAsync(ex, cancellationToken);
            }
        }
    }
}