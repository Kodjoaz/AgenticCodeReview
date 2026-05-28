using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions
{
    /// <summary>
    /// Extensions DI pour l'enregistrement de producers avec liaison TMessage → target.
    /// Élimine le besoin de créer des sous-classes vides (ex: DipensateurProducer)
    /// et de passer le target au constructeur.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class ProducerServiceCollectionExtensions
    {
        /// <summary>
        /// Enregistre un <see cref="IMessageProducer{TMessage}"/> avec le target spécifié.
        /// Le target est résolu automatiquement lors de PublishAsync — pas besoin de le passer à l'appel.
        /// </summary>
        /// <typeparam name="TMessage">Type du message. Chaque type est lié à un seul target.</typeparam>
        /// <param name="services">Collection de services DI.</param>
        /// <param name="target">Identifiant logique du target (ex: "dispensateur", "individu").</param>
        /// <returns>La collection de services pour chaîner les appels.</returns>
        /// <example>
        /// <code>
        /// services.AddProducer&lt;MessageDispensateur&gt;("dispensateur");
        /// services.AddProducer&lt;MessageIndividu&gt;("individu");
        /// services.AddProducer&lt;MessagePharmacie&gt;("pharmacie");
        /// </code>
        /// </example>
        public static IServiceCollection AddProducer<TMessage>(
            this IServiceCollection services,
            string target) where TMessage : class
        {
            ArgumentNullException.ThrowIfNull(target);

            // Singleton du TargetMap (enregistré une seule fois)
            services.TryAddSingleton<IMessageTargetMap, MessageTargetMap>();

            // Accumulation des liaisons TMessage → target via le pattern Configure/IOptions
            services.Configure<MessageTargetMapOptions>(o => o.Map<TMessage>(target));

            // Producer concret sous IMessageProducer<TMessage> (Scoped = 1 par requête)
            services.TryAddScoped<IMessageProducer<TMessage>, Producer<TMessage>>();

            return services;
        }

        /// <summary>
        /// Enregistre un <see cref="IMessageProducer{TMessage}"/> sans target fixe
        /// (mono-audience ou target passé à l'appel via PublishOptions).
        /// </summary>
        public static IServiceCollection AddProducer<TMessage>(
            this IServiceCollection services) where TMessage : class
        {
            services.TryAddSingleton<IMessageTargetMap, MessageTargetMap>();
            services.TryAddScoped<IMessageProducer<TMessage>, Producer<TMessage>>();

            return services;
        }

        /// <summary>
        /// Enregistre un <see cref="IRequestReplyClient{TRequest, TResponse}"/> avec les targets
        /// de l'endpoint de requête et de l'endpoint de réponse.
        /// </summary>
        /// <typeparam name="TRequest">Type du message requête envoyé au responder.</typeparam>
        /// <typeparam name="TResponse">Type du message réponse attendu du responder.</typeparam>
        /// <param name="requestTarget">Target logique de l'endpoint de requête (ex: "request-queue").</param>
        /// <param name="replyTarget">Target logique de l'endpoint de réponse (ex: "reply-queue").</param>
        /// <example>
        /// <code>
        /// services.AddRequestReplyClient&lt;RequestMessage, ReplyMessage&gt;("request-queue", "reply-queue");
        /// </code>
        /// </example>
        public static IServiceCollection AddRequestReplyClient<TRequest, TResponse>(
            this IServiceCollection services,
            string requestTarget,
            string replyTarget)
            where TRequest : class
            where TResponse : class
        {
            ArgumentNullException.ThrowIfNull(requestTarget);
            ArgumentNullException.ThrowIfNull(replyTarget);

            services.TryAddSingleton<IMessageTargetMap, MessageTargetMap>();
            services.Configure<MessageTargetMapOptions>(o =>
            {
                o.Map<TRequest>(requestTarget);
                o.Map<TResponse>(replyTarget);
            });
            services.TryAddScoped<IRequestReplyClient<TRequest, TResponse>,
                AzureRequestReplyClient<TRequest, TResponse>>();

            return services;
        }
    }
}
