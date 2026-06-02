using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Contexte fourni par le framework à votre activité au moment de l'exécution.
    /// Construit par <see cref="RoutingSlipExecutor"/> — vous ne le construisez jamais directement.
    /// </summary>
    /// <typeparam name="TArgs">Type des arguments spécifiques à cette étape.</typeparam>
    public sealed class ActivityContext<TArgs> where TArgs : class
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Arguments spécifiques à cette étape, définis par l'activateur via RoutingSlipBuilder.AddStep().
        /// Immuables — ne peuvent pas être modifiés pendant l'exécution.
        /// </summary>
        public required TArgs Arguments { get; init; }

        /// <summary>
        /// Variables partagées entre TOUTES les étapes du slip.
        /// Enrichies par chaque étape via <c>ActivityResult.Next(vars => ...)</c>.
        /// Utilisez <see cref="GetVariable{T}"/> pour lire une valeur typée.
        /// NE CASTEZ JAMAIS directement : après un round-trip JSON les valeurs sont des JsonElement.
        /// </summary>
        public IReadOnlyDictionary<string, JsonElement> Variables { get; init; }
            = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Lit une variable partagée de façon typée et sécurisée via System.Text.Json.
        /// Retourne null si la clé est absente ou si la valeur est null JSON.
        /// </summary>
        /// <example>
        ///   var date = ctx.GetVariable&lt;DateTime&gt;("DateValidation");
        ///   var nom  = ctx.GetVariable&lt;string&gt;("NomBeneficiaire");
        /// </example>
        public T? GetVariable<T>(string key)
        {
            if (!Variables.TryGetValue(key, out var element)) return default;
            return element.Deserialize<T>(_jsonOptions);
        }

        /// <summary>
        /// Token Claim-Check propagé depuis le message Service Bus.
        /// Non null uniquement si le SlipEnvelope dépasse 256 Ko et qu'EMT a appliqué un claim-check.
        /// L'activité (ou son API downstream) décide comment consommer le payload :
        /// - Option A : passer la référence blob à l'API downstream directement.
        /// - Option B : télécharger via IStorageProvider si l'API ne supporte pas les blobs.
        /// </summary>
        public TokenMessage? ClaimCheckToken { get; init; }

        /// <summary>
        /// Identifiant unique du slip — identique du début à la fin du workflow.
        /// Utilisez-le dans les logs pour retracer tout le parcours.
        /// </summary>
        public required string SlipId { get; init; }

        /// <summary>Identifiant de corrélation EMT — propagé automatiquement à chaque hop.</summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Nombre de tentatives de livraison pour l'étape courante (1-basé, depuis DeliveryCount broker).
        /// Si Attempt > 1, le message est rejoué — utile pour les logs de diagnostic.
        /// </summary>
        public int Attempt { get; init; }

        /// <summary>
        /// Nom logique de l'étape = valeur du Target dans AppSettings.Endpoints = stepName donné à AddStep().
        /// Utile pour les logs : _logger.LogInformation("Étape {Step}", ctx.StepName)
        /// </summary>
        public required string StepName { get; init; }

        /// <summary>Index 0-basé. StepIndex=0 = première étape.</summary>
        public int StepIndex { get; init; }

        /// <summary>Nombre total d'étapes dans le slip.</summary>
        public int TotalSteps { get; init; }
    }
}
