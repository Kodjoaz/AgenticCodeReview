using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;

namespace RAMQ.COM.EnterpriseMessageTransit.Serialization
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly IMessageTransitConfigurationService _config;
        private readonly ILogger<JsonMessageSerializer> _logger;

        // Safety defaults; these can be tuned later or exposed via configuration.
        private const int DefaultMaxDepth = 64;
        private const int DefaultMaxJsonLength = 1_000_000; // 1 MB

        // Cache JsonSerializerOptions instances to avoid recreating them per call.
        private static readonly JsonSerializerOptions s_serializeOptionsIndented = new JsonSerializerOptions { WriteIndented = true };
        private static readonly JsonSerializerOptions s_serializeOptionsCompact = new JsonSerializerOptions { WriteIndented = false };

        static JsonMessageSerializer()
        {
            s_serializeOptionsIndented.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            s_serializeOptionsCompact.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        }
        private static readonly JsonSerializerOptions s_deserializeOptions = new JsonSerializerOptions { MaxDepth = DefaultMaxDepth };

        public JsonMessageSerializer(IMessageTransitConfigurationService config, ILogger<JsonMessageSerializer> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Serialize<TMessage>(TMessage obj) where TMessage : class
        {
            var enableIndent = _config.AppSettings?.EnableJsonIndentation ?? false;
            var options = enableIndent ? s_serializeOptionsIndented : s_serializeOptionsCompact;
            return JsonSerializer.Serialize(obj, options);
        }

        public TMessage? Deserialize<TMessage>(string json) where TMessage : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Attempted to deserialize empty or whitespace JSON into {TypeName}.", typeof(TMessage).FullName);
                return null;
            }

            if (json.Length > DefaultMaxJsonLength)
            {
                _logger.LogWarning("JSON payload too large ({Length} chars) trying to deserialize to {TypeName}; max allowed is {Max}.", json.Length, typeof(TMessage).FullName, DefaultMaxJsonLength);
                return null;
            }

            try
            {
                // Single deserialization pass. Pre-validation (JsonDocument.Parse) was eliminated
                // for performance — messages from Azure Service Bus are considered trusted.
                // If strict validation is needed, enable via configuration or override this method.
                return JsonSerializer.Deserialize<TMessage>(json, s_deserializeOptions);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "Deserialization failed for type {TypeName}: {Message}", typeof(TMessage).FullName, jex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during deserialization for type {TypeName}: {Message}", typeof(TMessage).FullName, ex.Message);
                return null;
            }
        }

        public DeserializationResult<TMessage> DeserializeSafe<TMessage>(string? data) where TMessage : class
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                _logger.LogWarning("DeserializeSafe: empty or whitespace JSON for {TypeName}.", typeof(TMessage).FullName);
                return DeserializationResult<TMessage>.EmptyPayload();
            }

            if (data.Length > DefaultMaxJsonLength)
            {
                _logger.LogWarning("DeserializeSafe: payload too large ({Length} chars) for {TypeName}; max={Max}.", data.Length, typeof(TMessage).FullName, DefaultMaxJsonLength);
                return DeserializationResult<TMessage>.PayloadTooLarge(data.Length, DefaultMaxJsonLength);
            }

            try
            {
                var result = JsonSerializer.Deserialize<TMessage>(data, s_deserializeOptions);
                if (result == null)
                {
                    _logger.LogWarning("DeserializeSafe: JsonSerializer returned null for {TypeName}.", typeof(TMessage).FullName);
                    return DeserializationResult<TMessage>.EmptyPayload();
                }
                return DeserializationResult<TMessage>.Success(result);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "DeserializeSafe: malformed JSON for {TypeName}: {Message}", typeof(TMessage).FullName, jex.Message);
                return DeserializationResult<TMessage>.Malformed(jex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeserializeSafe: unexpected error for {TypeName}: {Message}", typeof(TMessage).FullName, ex.Message);
                return DeserializationResult<TMessage>.UnexpectedError(ex);
            }
        }
    }
}
