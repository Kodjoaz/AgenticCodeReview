using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions
{
    /// <summary>
    /// Extensions DI pour enregistrer les activités du routing slip v2.0.
    ///
    /// Chaque activité est liée à une étape (Target dans AppSettings.Endpoints).
    /// Le framework résout automatiquement l'activité et orchestre le routing slip à l'exécution.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class RoutingSlipServiceCollectionExtensions
    {
        /// <summary>
        /// Enregistre une activité de routing slip pour une étape Queue.
        /// </summary>
        /// <typeparam name="TActivity">Implémentation de l'activité (votre classe).</typeparam>
        /// <typeparam name="TArgs">Type des arguments que cette activité reçoit.</typeparam>
        /// <param name="services">Collection de services DI.</param>
        /// <param name="target">
        /// Nom logique de l'étape = valeur du champ Target dans AppSettings.Endpoints.
        /// Doit correspondre exactement au stepName utilisé dans RoutingSlipBuilder.AddStep().
        /// </param>
        /// <returns>La collection de services pour chaîner les appels.</returns>
        /// <example>
        /// <code>
        /// // Dans Program.cs :
        /// services.AddRoutingSlipActivity&lt;ValiderAdmissibiliteActivity, ValiderArgs&gt;("ValiderAdmissibilite");
        /// services.AddRoutingSlipActivity&lt;EnrichirDonneesActivity, EnrichirArgs&gt;("EnrichirDonnees");
        ///
        /// // Dans le Function trigger (résolution par clé = typeof(TArgs)) :
        /// public async Task Run(
        ///     [ServiceBusTrigger(...)] ServiceBusReceivedMessage message,
        ///     [FromKeyedServices(typeof(ValiderArgs))] IRoutingSlipExecutor executor,
        ///     CancellationToken ct)
        /// {
        ///     await executor.ProcessAsync(_provider, ct);
        /// }
        /// </code>
        /// </example>
        public static IServiceCollection AddRoutingSlipActivity<TActivity, TArgs>(
            this IServiceCollection services,
            string target)
            where TActivity : class, IRoutingSlipActivity<TArgs>
            where TArgs : class
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("target est requis.", nameof(target));

            // Activité — Scoped (1 par requête HTTP/message)
            services.TryAddScoped<IRoutingSlipActivity<TArgs>, TActivity>();

            // Exécuteur associé — Scoped, clé = typeof(TArgs).
            // AddKeyedScoped (et non TryAddScoped sur IRoutingSlipExecutor non-générique) permet
            // d'enregistrer plusieurs activités dans la même Function App sans collision :
            // chaque Worker résout son executor via [FromKeyedServices(typeof(TArgs))].
            services.AddKeyedScoped<IRoutingSlipExecutor, RoutingSlipExecutor<TArgs>>(typeof(TArgs));

            return services;
        }

        /// <summary>
        /// Enregistre une activité de routing slip pour une étape Topic.
        /// Le comportement est identique à <see cref="AddRoutingSlipActivity{TActivity,TArgs}"/> —
        /// la distinction Queue/Topic est gérée au niveau du Worker (ProcessAsync vs ExecuteAsync).
        /// </summary>
        /// <typeparam name="TActivity">Implémentation de l'activité (votre classe).</typeparam>
        /// <typeparam name="TArgs">Type des arguments que cette activité reçoit.</typeparam>
        /// <param name="services">Collection de services DI.</param>
        /// <param name="target">
        /// Nom logique de l'étape = valeur du champ Target dans AppSettings.Endpoints.
        /// </param>
        public static IServiceCollection AddRoutingSlipActivityForTopic<TActivity, TArgs>(
            this IServiceCollection services,
            string target)
            where TActivity : class, IRoutingSlipActivity<TArgs>
            where TArgs : class
            => services.AddRoutingSlipActivity<TActivity, TArgs>(target);
    }
}
