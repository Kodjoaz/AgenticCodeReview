using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Le "bon de livraison" qui voyage de queue en queue à travers le workflow.
    /// Contient l'itinéraire complet, le curseur courant, et les variables partagées.
    ///
    /// Auto-porteur : chaque SlipStep contient EntityName, EntityType et Subscription résolus.
    /// Les workers n'ont jamais besoin de la config de l'activateur pour router.
    ///
    /// Sérialisé comme payload du MessageTransitContext&lt;SlipEnvelope&gt; transporté par EMT.
    /// </summary>
    public sealed class SlipEnvelope
    {
        /// <summary>Métadonnées du slip : SlipId, SlipName, CorrelationId, CreatedAt.</summary>
        [JsonPropertyName("header")]
        public required SlipHeader Header { get; init; }

        /// <summary>
        /// Toutes les étapes dans l'ordre de définition par l'activateur.
        /// Steps[Cursor] = étape courante.
        /// Steps[Cursor + 1] = étape suivante.
        /// </summary>
        [JsonPropertyName("steps")]
        public required IReadOnlyList<SlipStep> Steps { get; init; }

        /// <summary>
        /// Index 0-basé de l'étape en cours de traitement.
        /// Incrémenté par RoutingSlipExecutor après chaque Next().
        /// </summary>
        [JsonPropertyName("cursor")]
        public int Cursor { get; init; }

        /// <summary>
        /// Variables partagées entre toutes les étapes.
        /// Enrichies à chaque Next(vars => ...) par fusion (merge).
        /// Lues avec ctx.GetVariable&lt;T&gt;() dans les activités.
        /// </summary>
        [JsonPropertyName("variables")]
        public Dictionary<string, JsonElement> Variables { get; init; }
            = new(StringComparer.OrdinalIgnoreCase);

        // ─── Helpers ────────────────────────────────────────────────────────

        /// <summary>Retourne l'étape courante (Steps[Cursor]).</summary>
        [JsonIgnore]
        public SlipStep CurrentStep => Steps[Cursor];

        /// <summary>True si l'étape courante est la dernière du slip.</summary>
        [JsonIgnore]
        public bool IsLastStep => Cursor == Steps.Length - 1;
    }
}
