using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;

namespace RAMQ.Samples.Queue.MultiTarget.Producer
{
    /// <summary>
    /// Classe de base abstraite pour les producers multi-target.
    /// Factorise la logique de publication : chaque sous-classe reconnaît son target
    /// et crée le contexte typé approprié.
    ///
    /// Patron Strategy (adapté de RCP-EmetteurBaseAzureFonction/Multitarget) :
    ///   ajouter un target = hériter de cette classe + enregistrer en DI.
    /// </summary>
    /// <typeparam name="TMessage">Type du message publié vers ce target.</typeparam>
    public abstract class MultiTargetProducer<TMessage>(
        IMessageProducer<TMessage> producer,
        ILogger logger)
        : IMultiTargetProducer where TMessage : class
    {
        /// <summary>Nom logique du target géré par ce producer (ex: "Target1").</summary>
        protected abstract string NomTarget { get; }

        /// <summary>
        /// Crée le contexte de message si ce producer reconnaît le target.
        /// Retourne null si le target ne correspond pas à ce producer.
        /// </summary>
        protected abstract MessageTransitContext<TMessage>? CreerContexte(string target, Guid id, string content);

        /// <inheritdoc />
        public async Task<bool> TryPublishAsync(string target, Guid id, string content, CancellationToken cancellationToken = default)
        {
            var contexte = CreerContexte(target, id, content);
            if (contexte == null)
            {
                return false;
            }
                

            var retour = await producer.PublishAsync(contexte, (PublishOptions?)null, cancellationToken);
            logger.LogInformation(
                "Message publié vers {Target}. MessageId: {MessageId}",
                NomTarget, retour.MessageId);
            return true;
        }
    }
}
