using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using Microsoft.Extensions.Options;

namespace RAMQ.Samples.ConfigurationService
{
    public abstract class BaseConfigurationService
    {
        public BlobStorageSetting? BlobStorageSetting { get; }
        public AppSettings? AppSettings { get; }

        protected BaseConfigurationService(
            IOptions<BlobStorageSetting> blobStorageOptions,
            IOptions<AppSettings> appSettingOptions)
        {
            ArgumentNullException.ThrowIfNull(blobStorageOptions);
            ArgumentNullException.ThrowIfNull(appSettingOptions);
            BlobStorageSetting = blobStorageOptions.Value;
            AppSettings = appSettingOptions.Value;
        }
    }

    public class ConsumerConfigurationService : BaseConfigurationService, IConsumerConfigurationService
    {
        public ConsumerConfigurationService(
            IOptions<BlobStorageSetting> blobStorageOptions,
            IOptions<AppSettings> appSettingOptions)
            : base(blobStorageOptions, appSettingOptions) { }
    }

    public class ProducerConfigurationService : BaseConfigurationService, IProducerConfigurationService
    {
        public ProducerConfigurationService(
            IOptions<BlobStorageSetting> blobStorageOptions,
            IOptions<AppSettings> appSettingOptions)
            : base(blobStorageOptions, appSettingOptions) { }
    }
}

