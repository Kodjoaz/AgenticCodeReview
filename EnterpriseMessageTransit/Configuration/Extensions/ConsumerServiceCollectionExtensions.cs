using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions
{
    /// <summary>
    /// Extensions DI pour l'enregistrement de consumers avec liaison Consumer → target / consumer / action.
    /// Symétrique à <see cref="ProducerServiceCollectionExtensions"/> pour garantir
    /// une résolution correcte du target en configuration multi-endpoint, et propager
    /// le contexte consumer/action au journal et au retry handler.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class ConsumerServiceCollectionExtensions
    {
        /// <summary>
        /// Enregistre un consumer concret (Scoped) avec tous les paramètres optionnels.
        /// <para>
        /// **Méthode recommandée** — utiliser celle-ci pour tous les cas d'usage.
        /// Les paramètres peuvent être partiellement omis en passant `null` ou en n'en fournissant que certains.
        /// </para>
        /// </summary>
        /// <example>
        /// <code>
        /// // Contexte complet
        /// services.AddConsumer&lt;CarConsumer&gt;("Car", "BookingConsumer", "ReserverVoiture");
        ///
        /// // Target + Action uniquement
        /// services.AddConsumer&lt;CarConsumer&gt;("Car", null, "ReserverVoiture");
        ///
        /// // Target uniquement
        /// services.AddConsumer&lt;CarConsumer&gt;("Car");
        ///
        /// // Aucun paramètre
        /// services.AddConsumer&lt;CarConsumer&gt;();
        /// </code>
        /// </example>
        public static IServiceCollection AddConsumer<TConsumer>(
            this IServiceCollection services,
            string? target = null,
            string? consumerName = null,
            string? actionName = null) where TConsumer : class
        {
            services.TryAddScoped<TConsumer>(sp =>
                ActivatorUtilities.CreateInstance<TConsumer>(sp, target ?? string.Empty, consumerName ?? string.Empty, actionName ?? string.Empty));
            return services;
        }

        /// <summary>
        /// Enregistre un consumer concret (Scoped) avec uniquement l'action.
        /// <para>
        /// **Cas spécifique** — utiliser seulement si vous avez besoin de passer l'action sans target ni consumer.
        /// Préférer <c>AddConsumer&lt;T&gt;(actionName: actionName)</c> pour plus de clarté.
        /// </para>
        /// </summary>
        public static IServiceCollection AddConsumerWithAction<TConsumer>(
            this IServiceCollection services,
            string actionName) where TConsumer : class
        {
            ArgumentNullException.ThrowIfNull(actionName);
            services.TryAddScoped<TConsumer>(sp =>
                ActivatorUtilities.CreateInstance<TConsumer>(sp, string.Empty, string.Empty, actionName));
            return services;
        }

        /// <summary>
        /// Enregistre un consumer concret (Scoped) avec le target et l'action.
        /// <para>
        /// **Cas spécifique** — utiliser seulement si vous avez besoin de target + action sans consumer.
        /// Préférer <c>AddConsumer&lt;T&gt;(target, actionName: actionName)</c> pour plus de clarté.
        /// </para>
        /// </summary>
        public static IServiceCollection AddConsumerWithTargetAndAction<TConsumer>(
            this IServiceCollection services,
            string target,
            string actionName) where TConsumer : class
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(actionName);
            services.TryAddScoped<TConsumer>(sp =>
                ActivatorUtilities.CreateInstance<TConsumer>(sp, target, string.Empty, actionName));
            return services;
        }

        /// <summary>
        /// Enregistre un consumer concret (Scoped) avec le target et le consumer.
        /// <para>
        /// **Cas spécifique** — utiliser seulement si vous avez besoin de target + consumer sans action.
        /// Préférer <c>AddConsumer&lt;T&gt;(target, consumer)</c> pour plus de clarté.
        /// </para>
        /// </summary>
        public static IServiceCollection AddConsumerWithTargetAndConsumer<TConsumer>(
            this IServiceCollection services,
            string target,
            string consumerName) where TConsumer : class
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(consumerName);
            services.TryAddScoped<TConsumer>(sp =>
                ActivatorUtilities.CreateInstance<TConsumer>(sp, target, consumerName, string.Empty));
            return services;
        }

        /// <summary>
        /// Enregistre un consumer concret (Scoped) avec le target, le consumer et l'action.
        /// <para>
        /// **Cas spécifique** — utiliser seulement si vous préférez une signature stricte (tous les paramètres obligatoires).
        /// Préférer <c>AddConsumer&lt;T&gt;(target, consumer, action)</c> pour plus de flexibilité.
        /// </para>
        /// </summary>
        public static IServiceCollection AddConsumerWithContext<TConsumer>(
            this IServiceCollection services,
            string target,
            string consumerName,
            string actionName) where TConsumer : class
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(consumerName);
            ArgumentNullException.ThrowIfNull(actionName);
            services.TryAddScoped<TConsumer>(sp =>
                ActivatorUtilities.CreateInstance<TConsumer>(sp, target, consumerName, actionName));
            return services;
        }
    }
}
