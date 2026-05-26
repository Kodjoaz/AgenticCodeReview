using System.Text.Json;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Construit un <see cref="SlipEnvelope"/> depuis des noms logiques (Target).
    ///
    /// Principe fondamental : aucun nom d'entité Service Bus dans le code.
    /// Utilisez des stepNames = Target dans AppSettings.Endpoints.
    /// L'EntityName physique est résolu par IEndpointResolver — jamais écrit à la main.
    /// </summary>
    /// <example>
    /// var slip = new RoutingSlipBuilder("TraiterDossier", _endpointResolver)
    ///     .AddStep("ValiderAdmissibilite", new ValiderArgs { DossierId = id })
    ///     .AddStep("EnrichirDonnees",      new EnrichirArgs { DossierId = id })
    ///     .AddStep("NotifierBeneficiaire", new NotifierArgs { Canal = "email" })
    ///     .Build();
    /// </example>
    public sealed class RoutingSlipBuilder
    {
        private static readonly JsonSerializerOptions _jsonOptions = new();

        private readonly string _slipName;
        private readonly IEndpointResolver _endpointResolver;
        private readonly List<(string StepName, EndpointSettings Endpoint, JsonElement Args)> _steps = new();

        /// <param name="slipName">Nom lisible du workflow. Apparaît dans les logs et métriques.</param>
        /// <param name="endpointResolver">Résout Target → EndpointSettings depuis AppSettings.Endpoints.</param>
        public RoutingSlipBuilder(string slipName, IEndpointResolver endpointResolver)
        {
            _slipName = string.IsNullOrWhiteSpace(slipName)
                ? throw new ArgumentException("slipName est requis.", nameof(slipName))
                : slipName;
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        }

        /// <summary>
        /// Ajoute une étape au slip.
        /// </summary>
        /// <param name="stepName">
        /// Nom logique = valeur du champ Target dans AppSettings.Endpoints.
        /// stepName == Target : ce sont exactement la même chose.
        /// </param>
        /// <param name="arguments">Arguments que cette étape recevra. Sérialisés en JSON.</param>
        /// <exception cref="TransitItineraryException">Si stepName ne correspond à aucun Target connu.</exception>
        public RoutingSlipBuilder AddStep<TArgs>(string stepName, TArgs arguments) where TArgs : class
        {
            if (string.IsNullOrWhiteSpace(stepName))
                throw new ArgumentException("stepName est requis.", nameof(stepName));
            ArgumentNullException.ThrowIfNull(arguments);

            if (!_endpointResolver.TryResolve(stepName, null, null, out var endpoint) || endpoint == null)
                throw new TransitItineraryException(
                    $"RoutingSlipBuilder.AddStep: Target '{stepName}' introuvable dans AppSettings.Endpoints. " +
                    $"Vérifiez que Endpoints[i].Target == \"{stepName}\" existe dans la configuration.");

            var argsJson = JsonSerializer.SerializeToElement(arguments, _jsonOptions);
            _steps.Add((stepName, endpoint, argsJson));
            return this;
        }

        /// <summary>
        /// Construit le <see cref="SlipEnvelope"/> prêt à être publié via IMessageProducer&lt;SlipEnvelope&gt;.
        /// Résout tous les stepName en EntityName/EntityType/Subscription.
        /// </summary>
        /// <exception cref="InvalidOperationException">Si aucune étape n'a été ajoutée.</exception>
        public SlipEnvelope Build()
        {
            if (_steps.Count == 0)
                throw new InvalidOperationException("RoutingSlipBuilder: au moins une étape est requise.");

            var slipId = Guid.NewGuid().ToString("D");
            var steps = _steps.Select((s, i) => new SlipStep
            {
                Name         = s.StepName,
                EntityName   = s.Endpoint.Endpoint?.EntityName
                               ?? throw new TransitItineraryException($"EndpointSettings.Endpoint.EntityName manquant pour '{s.StepName}'."),
                EntityType   = s.Endpoint.Endpoint?.EntityType ?? MessagingEntityType.Queue,
                Subscription = s.Endpoint.Endpoint?.Subscription != null
                    ? new SlipTopicSubscription
                      {
                          Consumer = s.Endpoint.Endpoint.Subscription.Consumer,
                          Action   = s.Endpoint.Endpoint.Subscription.Action
                      }
                    : null,
                Arguments    = s.Args,
                Status       = i == 0 ? SlipStepStatus.Active : SlipStepStatus.Pending
            }).ToArray();

            return new SlipEnvelope
            {
                Header = new SlipHeader
                {
                    SlipId    = slipId,
                    SlipName  = _slipName,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                Steps    = steps,
                Cursor   = 0,
                Variables = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
