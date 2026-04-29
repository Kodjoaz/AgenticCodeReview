using System.Text.Json.Serialization;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    /// <summary>
    /// Contexte de transit (refonte) sans couche de compatibilité legacy.
    /// CurrentStage remplace définitivement l’ancien CurrentTarget.
    /// </summary>
    /// <typeparam name="TMessage">Représente le contenu de l'événement</typeparam>
    public class MessageTransitContext<TMessage> where TMessage : class
    {
        public string? MessageType { get; set; }
        public TMessage? Message { get; set; }
        public string? MessageId { get; set; }
        public string? SessionId { get; set; }
        public long SequenceNumber { get; set; }
        public int Attempt { get; set; }

        [JsonPropertyName("CurrentStage")]
        public string? CurrentStage { get; internal set; }
        [JsonIgnore]
        public IMessageTransit? TransportMessage { get; set; }
        public List<TokenMessage>? Tokens { get; set; }
        public Dictionary<string, object>? Variables { get; set; }
        [JsonIgnore]
        public string? SerializedPayload { get; set; }
        [JsonIgnore]
        public bool IsClaimCheckApplied { get; set; }

        // Setter rendu internal (au lieu de private) pour permettre l’affectation dans BaseProducer / BaseConsumer.
        internal void SetCurrentStage(string? stage) => CurrentStage = stage;

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
                MessageId       = MessageId,
                SessionId       = SessionId,
                CurrentStage    = CurrentStage,
                SequenceNumber  = SequenceNumber,
                Attempt         = Attempt,
                Tokens          = Tokens,
                Variables       = Variables,
                TransportMessage = TransportMessage,
                Message         = response
            };
        }
    }
}
