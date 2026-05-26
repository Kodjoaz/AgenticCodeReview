using Microsoft.Extensions.Logging;

namespace RAMQ.Samples.Queue.MultiTarget.Producer
{
    /// <summary>
    /// Service de publication multi-target.
    /// Itère la chaîne de <see cref="IMultiTargetProducer"/> (Strategy) :
    /// le premier producer qui reconnaît le target publie le message.
    ///
    /// Patron Strategy (adapté de RCP-EmetteurBaseAzureFonction/Multitarget) :
    ///   ajouter un target = ajouter un <see cref="IMultiTargetProducer"/> + l'enregistrer en DI.
    /// </summary>
    public class MultiTargetPublicationService(
        IEnumerable<IMultiTargetProducer> producers,
        ILogger<MultiTargetPublicationService> logger)
    {
        /// <summary>
        /// Publie un message vers le target spécifié.
        /// Lance une exception si aucun producer ne reconnaît le target.
        /// </summary>
        public async Task PublierAsync(string target, Guid id, string content, CancellationToken cancellationToken = default)
        {
            foreach (var producer in producers)
            {
                if (await producer.TryPublishAsync(target, id, content, cancellationToken))
                    return;
            }

            throw new InvalidOperationException(
                $"Aucun producer ne gère le target '{target}'. " +
                $"Vérifiez l'enregistrement DI des IMultiTargetProducer.");
        }
    }
}
