namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Une étape du routing slip. Implémentez cette interface pour chaque étape de votre workflow.
    ///
    /// RÈGLE ARCHITECTURALE : l'activité est un orchestrateur de transit, pas un moteur métier.
    /// Toute la logique de traitement doit être déléguée à un service ou une API externe.
    /// Ne jamais appeler de méthodes EMT (CompleteMessageAsync, RouteToNextStageAsync, etc.) ici.
    /// Le framework gère automatiquement le routing après l'exécution.
    /// </summary>
    /// <typeparam name="TArgs">
    /// Le type de vos arguments. Chaque activité a son propre type — elles n'ont pas à partager
    /// le même type de message.
    /// </typeparam>
    public interface IRoutingSlipActivity<TArgs> where TArgs : class
    {
        Task<ActivityResult> ExecuteAsync(ActivityContext<TArgs> ctx, CancellationToken ct);
    }
}
