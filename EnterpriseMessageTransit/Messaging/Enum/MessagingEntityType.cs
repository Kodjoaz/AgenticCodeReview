using System.Text.Json.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum
{
    /// <summary>
    /// Type d'entité de messagerie — neutre par rapport au fournisseur (transport-agnostic).
    /// Remplace <c>ServiceBusEntityType</c> (anciennement dans le namespace Azure)
    /// pour permettre le support multi-fournisseur (Azure Service Bus, Confluent Kafka, etc.).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessagingEntityType
    {
        /// <summary>Aucun type spécifié.</summary>
        None = 0,

        /// <summary>Rubrique (Topic) — ex.: Azure Service Bus Topic, Kafka Topic.</summary>
        Topic = 1,

        /// <summary>File d'attente (Queue) — ex.: Azure Service Bus Queue.</summary>
        Queue = 2,

        /// <summary>
        /// Exchange — ex.: RabbitMQ Exchange.
        /// Réservé pour usage futur.
        /// </summary>
        Exchange = 3,

        /// <summary>
        /// Canal générique — ex.: SignalR Hub, canal personnalisé.
        /// Réservé pour usage futur.
        /// </summary>
        Channel = 4
    }
}
