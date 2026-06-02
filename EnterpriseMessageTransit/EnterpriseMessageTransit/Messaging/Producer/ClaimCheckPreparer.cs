using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Responsabilité unique : préparer le Claim Check avant publication.
    /// Upload le payload sérialisé en Blob si le seuil est dépassé et met à jour
    /// les tokens du contexte. Aucune logique de publication ou de journal ici.
    /// </summary>
    internal sealed class ClaimCheckPreparer : IClaimCheckPreparer
    {
        private readonly IStorageProvider _storage;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageTransitConfigurationService _config;
        private readonly IMetricsProvider? _metrics;
        private readonly ILogger<ClaimCheckPreparer> _logger;

        public ClaimCheckPreparer(
            IStorageProvider storage,
            IMessageSerializer serializer,
            IMessageTransitConfigurationService config,
            ILogger<ClaimCheckPreparer> logger,
            IMetricsProvider? metrics = null)
        {
            _storage    = storage    ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _config     = config     ?? throw new ArgumentNullException(nameof(config));
            _logger     = logger     ?? throw new ArgumentNullException(nameof(logger));
            _metrics    = metrics;
        }

        public async Task PrepareAsync<TMessage>(
            MessageTransitContext<TMessage> context,
            Stream? fileStream,
            string? originalFileName,
            bool forceClaimCheck,
            CancellationToken cancellationToken)
            where TMessage : class
        {
            context.Tokens ??= new List<TokenMessage>();

            string serialized = _serializer.Serialize(context);
            long   size       = System.Text.Encoding.UTF8.GetByteCount(serialized);

            if (RequiresClaimCheck((int)size, forceClaimCheck))
            {
                string fileName = string.IsNullOrWhiteSpace(context.MessageId)
                    ? $"{Guid.NewGuid():N}.json"
                    : $"{context.MessageId}.json";

                using var uploadActivity = MessagingActivitySource.Source.StartActivity(
                    "messaging.claimcheck.upload",
                    ActivityKind.Internal);
                uploadActivity?.SetTag("messaging.message_id",            context.MessageId);
                uploadActivity?.SetTag("messaging.claimcheck.blob",       fileName);
                uploadActivity?.SetTag("messaging.claimcheck.size_bytes", size);

                var sw = Stopwatch.StartNew();
                string url;
                try
                {
                    url = await _storage.UploadAsync(serialized, fileName, cancellationToken);
                    sw.Stop();
                    uploadActivity?.SetStatus(ActivityStatusCode.Ok);
                    _metrics?.RecordClaimCheckUploadDuration(sw.Elapsed.TotalMilliseconds, fileName);
                    _metrics?.IncrementClaimCheckUploads(fileName);
                }
                catch (Exception ex)
                {
                    uploadActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    uploadActivity?.SetTag("exception.type",    ex.GetType().FullName);
                    uploadActivity?.SetTag("exception.message", ex.Message);
                    throw;
                }

                var reference = NormalizeBlobReference(url) ?? url;
                uploadActivity?.SetTag("messaging.claimcheck.reference", reference);

                context.Tokens.Add(new TokenMessage
                {
                    Kind        = TokenKind.Message,
                    ContentType = "application/json",
                    Reference   = reference,
                    SizeBytes   = size
                });
                context.Message           = default;
                context.SerializedPayload = null;
                context.IsClaimCheckApplied = true;
            }
            else
            {
                context.SerializedPayload   = serialized;
                context.IsClaimCheckApplied = false;
            }

            if (fileStream != null && !string.IsNullOrWhiteSpace(originalFileName))
            {
                string? url = await _storage.UploadAsync(fileStream, originalFileName, cancellationToken);
                var reference = NormalizeBlobReference(url) ?? url ?? string.Empty;

                long sizeBytes;
                try   { sizeBytes = fileStream.Length; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ClaimCheckPreparer: unable to determine fileStream.Length; using -1.");
                    sizeBytes = -1;
                }

                context.Tokens.Add(new TokenMessage
                {
                    Kind        = TokenKind.File,
                    ContentType = "application/octet-stream",
                    Reference   = reference,
                    SizeBytes   = sizeBytes
                });
            }
        }

        private bool RequiresClaimCheck(int sizeInBytes, bool force)
        {
            int threshold = _config.BlobStorageSetting?.ClaimCheckThresholdBytes ?? 256 * 1024;
            return force || sizeInBytes >= threshold;
        }

        private static string? NormalizeBlobReference(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            raw = raw.Trim();
            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                var path     = uri.AbsolutePath.TrimStart('/');
                var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                    return $"{segments[0]}/{string.Join("/", segments.Skip(1))}";
                return path;
            }
            return raw;
        }
    }
}
