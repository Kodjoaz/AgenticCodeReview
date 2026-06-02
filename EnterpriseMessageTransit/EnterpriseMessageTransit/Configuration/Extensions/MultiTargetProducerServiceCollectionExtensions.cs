using Microsoft.Extensions.DependencyInjection;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions
{
    /// <summary>
    /// Extensions DI pour enregistrer un <see cref="IMultiTargetProducer{TBase}"/> typé.
    /// </summary>
    public static class MultiTargetProducerServiceCollectionExtensions
    {
        /// <summary>
        /// Enregistre un <see cref="IMultiTargetProducer{TBase}"/> qui route chaque
        /// <c>TPayload</c> vers la cible logique configurée dans le builder.
        /// </summary>
        /// <typeparam name="TBase">
        /// Type de base commun à tous les payloads (interface marqueur, classe abstraite, ou <c>object</c>).
        /// </typeparam>
        /// <param name="services">La collection de services DI.</param>
        /// <param name="configure">
        /// Action de configuration qui appelle <c>.AddTarget&lt;TPayload&gt;("target")</c>
        /// pour chaque type de message à router.
        /// </param>
        /// <example>
        /// <code>
        /// services.AddMultiTargetProducer&lt;IBookingMessage&gt;(b =&gt; b
        ///     .AddTarget&lt;CarMessage&gt;("Car")
        ///     .AddTarget&lt;HotelMessage&gt;("Hotel")
        ///     .AddTarget&lt;FlightMessage&gt;("Flight"));
        /// </code>
        /// </example>
        public static IServiceCollection AddMultiTargetProducer<TBase>(
            this IServiceCollection services,
            Action<MultiTargetBuilder<TBase>> configure)
            where TBase : class
        {
            ArgumentNullException.ThrowIfNull(configure);

            var builder = new MultiTargetBuilder<TBase>(services);
            configure(builder);

            var targets = (IReadOnlyDictionary<Type, string>)builder.Targets;
            services.AddSingleton<IMultiTargetProducer<TBase>>(sp =>
                new EmtMultiTargetProducer<TBase>(sp, targets));

            return services;
        }
    }

    /// <summary>
    /// Builder fluide pour configurer les cibles d'un <see cref="IMultiTargetProducer{TBase}"/>.
    /// </summary>
    /// <typeparam name="TBase">Type de base commun à tous les payloads.</typeparam>
    public sealed class MultiTargetBuilder<TBase> where TBase : class
    {
        private readonly IServiceCollection _services;

        internal MultiTargetBuilder(IServiceCollection services)
        {
            _services = services;
        }

        internal Dictionary<Type, string> Targets { get; } = new();

        /// <summary>
        /// Lie le type <typeparamref name="TPayload"/> à une cible logique.
        /// Appelle automatiquement <c>AddProducer&lt;TPayload&gt;(target)</c>
        /// — aucun enregistrement supplémentaire n'est nécessaire.
        /// </summary>
        /// <param name="target">
        /// Nom de la cible logique — doit correspondre à un <c>Target</c>
        /// dans <c>AppSettings.Endpoints</c>.
        /// </param>
        public MultiTargetBuilder<TBase> AddTarget<TPayload>(string target)
            where TPayload : class, TBase
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("La cible ne peut pas être vide.", nameof(target));

            _services.AddProducer<TPayload>(target);
            Targets[typeof(TPayload)] = target;
            return this;
        }
    }
}
