using System.Text.Json.Serialization;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    /// <summary>
    /// Contexte de transit — enveloppe pivot entre Producer et Consumer.
    /// Seuls les champs nécessaires au transport et à la traçabilité bout-en-bout voyagent en JSON.
    /// </summary>
    /// <typeparam name="TMessage">Représente le contenu de l'événement</typeparam>
    public class MessageTransitContext<TMessage> where TMessage : class
    {
        public string? MessageType { get; set; }
        public TMessage? Message { get; set; }
        public string? MessageId { get; set; }

        /// <summary>
        /// Identifiant de corrélation immuable qui ne change jamais, même lors des retries.
        /// Initialisé à la même valeur que MessageId lors de la publication.
        /// Sur un retry exponentiel (no-session), MessageId est régénéré mais CorrelationId
        /// continue de référencer le MessageId original pour la traçabilité de bout en bout.
        /// </summary>
        public string? CorrelationId { get; set; }

        public string? SessionId { get; set; }

        /// <summary>
        /// Numéro de séquence Service Bus. Affecté par le broker à la réception.
        /// Omis de la sérialisation JSON lorsque sa valeur est 0 (côté Producer).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Numéro de tentative courante (retry). Incrémenté par EMT à chaque cycle de retry.
        /// Omis de la sérialisation JSON lorsque sa valeur est 0 (premier essai).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Attempt { get; set; }

        [JsonIgnore]
        public IMessageTransit? TransportMessage { get; set; }
        public List<TokenMessage>? Tokens { get; set; }
        public Dictionary<string, object>? Variables { get; set; }
        [JsonIgnore]
        public string? SerializedPayload { get; set; }
        [JsonIgnore]
        public bool IsClaimCheckApplied { get; set; }

        public TData? GetVariable<TData>(string key)
        {
            if (Variables != null &&
                Variables.TryGetValue(key, out var value) &&
                value is TData tValue)
            {
                return tValue;
            }
            return default;
        }

        public TokenMessage? GetMessageToken() => Tokens?.Find(t => t.Kind == TokenKind.Message);
        public TokenMessage? GetFileToken() => Tokens?.Find(t => t.Kind == TokenKind.File);
        public List<TokenMessage> GetTokens() => Tokens ?? new List<TokenMessage>();

        public TData? GetApplicationPropertyValue<TData>(string key, TData? defaultValue = default)
        {
            if (Variables != null &&
                Variables.TryGetValue(key, out var value) &&
                value is TData cast)
            {
                return cast;
            }
            return defaultValue;
        }

        /// <summary>
        /// Crée un nouveau contexte de type <typeparamref name="TResponse"/> en copiant toutes les métadonnées
        /// du contexte courant et en substituant le message par <paramref name="response"/>.
        /// Garantit qu'un ajout de propriété à <see cref="MessageTransitContext{TMessage}"/> est
        /// automatiquement propagé sans oubli dans le mapping.
        /// </summary>
        public MessageTransitContext<TResponse> CopyWithResponse<TResponse>(TResponse? response)
            where TResponse : class
        {
            return new MessageTransitContext<TResponse>
            {
                MessageId        = MessageId,
                CorrelationId    = CorrelationId,
                SessionId        = SessionId,
                SequenceNumber   = SequenceNumber,
                Attempt          = Attempt,
                Tokens           = Tokens,
                Variables        = Variables,
                TransportMessage = TransportMessage,
                Message          = response
            };
        }
    }
}
