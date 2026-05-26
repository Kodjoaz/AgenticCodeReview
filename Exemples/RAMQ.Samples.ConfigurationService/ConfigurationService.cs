using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RAMQ.Samples.ConfigurationService
{
    public class ConsumerConfigurationService : IConsumerConfigurationService
    {
        public BlobStorageSetting? BlobStorageSetting { get; }
        public AppSettings? AppSettings { get; }

        private readonly ILogger<ConsumerConfigurationService> _logger;

        public ConsumerConfigurationService(
            IOptions<BlobStorageSetting> blobStorageOptions,
            IOptions<AppSettings> appSettingOptions,
            ILogger<ConsumerConfigurationService> logger)
        {
            BlobStorageSetting = blobStorageOptions.Value;
            AppSettings = appSettingOptions.Value;
            _logger = logger;
        }
    }

    public class ProducerConfigurationService : IProducerConfigurationService
    {
        public BlobStorageSetting? BlobStorageSetting { get; }
        public AppSettings? AppSettings { get; }

        private readonly ILogger<ProducerConfigurationService> _logger;

        public ProducerConfigurationService(
            IOptions<BlobStorageSetting> blobStorageOptions,
            IOptions<AppSettings> appSettingOptions,
            ILogger<ProducerConfigurationService> logger)
        {
            BlobStorageSetting = blobStorageOptions.Value;
            AppSettings = appSettingOptions.Value;
            _logger = logger;
        }
    }
}

