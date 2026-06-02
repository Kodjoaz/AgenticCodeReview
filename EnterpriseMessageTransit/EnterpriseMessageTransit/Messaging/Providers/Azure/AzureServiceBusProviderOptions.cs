namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    public class AzureServiceBusProviderOptions
    {
        public int MaxConcurrentSessions { get; set; } = 10;
        public int MaxConcurrentCallsPerSession { get; set; } = 1;
        public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public int MaxMessageSize { get; set; } = 256 * 1024;
        public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}
