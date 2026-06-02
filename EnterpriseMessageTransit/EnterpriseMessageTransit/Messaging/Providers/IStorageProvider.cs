namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    public interface IStorageProvider
    {
        Task<string> UploadAsync(string content, string fileName, CancellationToken cancellationToken);
        Task<string> UploadAsync(Stream stream, string fileName, CancellationToken cancellationToken);

        // Download the content referenced by a token (either absolute blob URI or relative "container/blob" reference).
        Task<Stream> DownloadAsync(string reference, CancellationToken cancellationToken);

        // Delete the referenced content (best-effort).
        Task DeleteAsync(string reference, CancellationToken cancellationToken);
    }
}
