namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public interface IMessageTransitConfigurationService
    {
        BlobStorageSetting? BlobStorageSetting { get; }
        AppSettings? AppSettings { get; }
    }
}
