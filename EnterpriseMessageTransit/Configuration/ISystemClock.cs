using System;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    /// <summary>
    /// Abstraction for system time to allow deterministic tests.
    /// </summary>
    public interface ISystemClock
    {
        DateTimeOffset UtcNow { get; }
    }
}
