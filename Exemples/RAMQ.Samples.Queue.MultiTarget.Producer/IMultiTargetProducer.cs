namespace RAMQ.Samples.Queue.MultiTarget.Producer
{
    /// <summary>
    /// Stratégie de production pour un target donné.
    /// Chaque implémentation reconnaît son target et publie le type de message correspondant.
    /// Patron Strategy : ajouter un nouveau target = créer un nouveau IMultiTargetProducer.
    /// </summary>
    public interface IMultiTargetProducer
    {
        /// <summary>
        /// Tente de publier vers ce target.
        /// </summary>
        /// <param name="target">Nom logique du target ("Target1", "Target2", "Target3").</param>
        /// <param name="id">Identifiant du message.</param>
        /// <param name="content">Contenu du message.</param>
        /// <param name="cancellationToken">Jeton d'annulation.</param>
        /// <returns>true si ce producer gère ce target et a publié ; false sinon.</returns>
        Task<bool> TryPublishAsync(string target, Guid id, string content, CancellationToken cancellationToken = default);
    }
}
