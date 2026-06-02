using System;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    /// <summary>
    /// Default system clock using DateTimeOffset.UtcNow.
    /// </summary>
    public class DefaultSystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
