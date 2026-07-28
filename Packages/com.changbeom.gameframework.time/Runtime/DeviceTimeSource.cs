using System;

namespace GameFramework.TimeSystem
{
    /// <summary>Raw device clock. Never trusted on its own since the OS clock can be changed by the user.</summary>
    public sealed class DeviceTimeSource : ITimeSource
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public bool IsTrusted => false;
    }
}
