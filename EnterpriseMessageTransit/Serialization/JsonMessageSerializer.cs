using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;

namespace RAMQ.COM.EnterpriseMessageTransit.Serialization
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly IMessageTransitConfigurationService _config;
        private readonly ILogger<JsonMessageSerializer> _logger;

        private const int DefaultMaxDepth      = 64;
        private const int DefaultMaxJsonLength = 1_000_000; // 1 Mo

        private static readonly JsonSerializerOptions s_serializeOptionsIndented = new() { WriteIndented = true };
        private static readonly JsonSerializerOptions s_serializeOptionsCompact  = new() { WriteIndented = false };

        static JsonMessageSerializer()
        {
            s_serializeOptionsIndented.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            s_serializeOptionsCompact.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        }

        private static readonly JsonSerializerOptions s_deserializeOptions = new() { MaxDepth = DefaultMaxDepth };

        public JsonMessageSerializer(IMessageTransitConfigurationService config, ILogger<JsonMessageSerializer> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Serialize<TMessage>(TMessage obj) where TMessage : class
        {
            var options = (_config.AppSettings?.EnableJsonIndentation ?? false)
                ? s_serializeOptionsIndented
                : s_serializeOptionsCompact;
            return JsonSerializer.Serialize(obj, options);
        }

        public TMessage? Deserialize<TMessage>(string json) where TMessage : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                // Debug : précondition non remplie — ne constitue pas une erreur opérationnelle.
                _logger.LogDebug("Désérialisation ignorée : JSON vide ou null pour {TypeName}.", typeof(TMessage).FullName);
                return null;
            }

            if (json.Length > DefaultMaxJsonLength)
            {
                // Warning : payload anormalement volumineux — à investiguer.
                _logger.LogWarning("Payload trop volumineux ({Longueur} caractères) pour la désérialisation en {TypeName} (max : {Max}).",
                    json.Length, typeof(TMessage).FullName, DefaultMaxJsonLength);
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<TMessage>(json, s_deserializeOptions);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "Échec de désérialisation JSON pour le type {TypeName}.", typeof(TMessage).FullName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la désérialisation pour le type {TypeName}.", typeof(TMessage).FullName);
                return null;
            }
        }

        public DeserializationResult<TMessage> DeserializeSafe<TMessage>(string? data) where TMessage : class
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                _logger.LogDebug("DeserializeSafe : JSON vide ou null pour {TypeName}.", typeof(TMessage).FullName);
                return DeserializationResult<TMessage>.EmptyPayload();
            }

            if (data.Length > DefaultMaxJsonLength)
            {
                _logger.LogWarning("DeserializeSafe : payload trop volumineux ({Longueur} caractères) pour {TypeName} (max : {Max}).",
                    data.Length, typeof(TMessage).FullName, DefaultMaxJsonLength);
                return DeserializationResult<TMessage>.PayloadTooLarge(data.Length, DefaultMaxJsonLength);
            }

            try
            {
                var result = JsonSerializer.Deserialize<TMessage>(data, s_deserializeOptions);
                if (result == null)
                {
                    _logger.LogDebug("DeserializeSafe : le désérialiseur a retourné null pour {TypeName}.", typeof(TMessage).FullName);
                    return DeserializationResult<TMessage>.EmptyPayload();
                }
                return DeserializationResult<TMessage>.Success(result);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "DeserializeSafe : JSON malformé pour le type {TypeName}.", typeof(TMessage).FullName);
                return DeserializationResult<TMessage>.Malformed(jex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeserializeSafe : erreur inattendue pour le type {TypeName}.", typeof(TMessage).FullName);
                return DeserializationResult<TMessage>.UnexpectedError(ex);
            }
        }
    }
}
