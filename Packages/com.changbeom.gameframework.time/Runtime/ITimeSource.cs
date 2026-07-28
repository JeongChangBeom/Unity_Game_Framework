using System;

namespace GameFramework.TimeSystem
{
    public interface ITimeSource
    {
        DateTimeOffset UtcNow { get; }
        bool IsTrusted { get; }
    }
}
