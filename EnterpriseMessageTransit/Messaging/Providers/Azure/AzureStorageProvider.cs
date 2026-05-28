using Azure.Storage.Blobs;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    [ExcludeFromCodeCoverage]
    public class AzureStorageProvider : IStorageProvider
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IMessageTransitConfigurationService _config;
        private readonly IMetricsProvider? _metrics;

        public AzureStorageProvider(
            BlobServiceClient blobServiceClient,
            IMessageTransitConfigurationService config,
            IMetricsProvider? metrics = null)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _config            = config            ?? throw new ArgumentNullException(nameof(config));
            _metrics           = metrics;
        }

        private string GetContainerName()
        {
            var containerName = _config.BlobStorageSetting?.ContainerName;
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new InvalidOperationException(
                    "BlobStorageSetting.ContainerName doit être configuré dans appsettings.json. " +
                    "Aucune valeur par défaut n'est appliquée pour éviter d'écrire dans un conteneur non intentionnel.");
            }
            return containerName;
        }

        public async Task<string> UploadAsync(string content, string fileName, CancellationToken cancellationToken)
        {
            var folderName = _config.BlobStorageSetting?.FolderName;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new InvalidOperationException("BlobStorageSetting.FolderName doit être configuré dans appsettings.json.");
            }
            var container = _blobServiceClient.GetBlobContainerClient(GetContainerName());
            var blobPath = $"{folderName.Trim('/')}/{fileName}";
            var blob = container.GetBlobClient(blobPath);
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await blob.UploadAsync(ms, overwrite: true, cancellationToken: cancellationToken);
            return blob.Uri.ToString();
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, CancellationToken cancellationToken)
        {
            var folderName = _config.BlobStorageSetting?.FolderName;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new InvalidOperationException("BlobStorageSetting.FolderName doit être configuré dans appsettings.json.");
            }
            var container = _blobServiceClient.GetBlobContainerClient(GetContainerName());
            var blobPath = $"{folderName.Trim('/')}/{fileName}";
            var blob = container.GetBlobClient(blobPath);

            // Correction : repositionner le flux au début
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            await blob.UploadAsync(fileStream, overwrite: true, cancellationToken: cancellationToken);
            return blob.Uri.ToString();
        }

        public async Task<Stream> DownloadAsync(string reference, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new ArgumentNullException(nameof(reference));
            }

            var sw = Stopwatch.StartNew();
            Stream result;

            if (Uri.TryCreate(reference, UriKind.Absolute, out var absoluteUri))
            {
                var blob = new BlobClient(absoluteUri);
                result = await blob.OpenReadAsync(cancellationToken: cancellationToken);
            }
            else
            {
                var parts = reference.TrimStart('/').Split(new[] { '/' }, 2);
                if (parts.Length < 2)
                    throw new InvalidOperationException("Invalid blob reference. Expected 'container/blobpath' or absolute URI.");

                var containerClient = _blobServiceClient.GetBlobContainerClient(parts[0]);
                var blobClient      = containerClient.GetBlobClient(parts[1]);
                result = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
            }

            sw.Stop();
            _metrics?.RecordClaimCheckDownloadDuration(sw.Elapsed.TotalMilliseconds, reference);
            _metrics?.IncrementClaimCheckDownloads(reference);
            return result;
        }

        public async Task DeleteAsync(string reference, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return;
            }

            if (Uri.TryCreate(reference, UriKind.Absolute, out var absoluteUri))
            {
                var blob = new BlobClient(absoluteUri);
                await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                return;
            }

            var parts = reference.TrimStart('/').Split(new[] { '/' }, 2);
            if (parts.Length < 2)
            {
                return;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(parts[0]);
            var blobClient = containerClient.GetBlobClient(parts[1]);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }
}
