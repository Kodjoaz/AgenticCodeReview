using System.Text.Json.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>Statut d'une étape dans le routing slip.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SlipStepStatus
    {
        /// <summary>Étape pas encore atteinte.</summary>
        Pending,
        /// <summary>Étape en cours de traitement.</summary>
        Active,
        /// <summary>Étape terminée avec succès.</summary>
        Completed,
        /// <summary>Étape terminée en erreur permanente (→ DLQ).</summary>
        Faulted
    }
}
