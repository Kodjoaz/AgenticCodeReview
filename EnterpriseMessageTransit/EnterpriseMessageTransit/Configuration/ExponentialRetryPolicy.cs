namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public class ExponentialRetryPolicy
    {
        // Use an explicit 500ms default (was mistakenly using an HttpStatusCode value)
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);
        public bool UseJitter { get; set; } = true;
        public int MaxDeliveryCount { get; set; } = 10;
    }
}
