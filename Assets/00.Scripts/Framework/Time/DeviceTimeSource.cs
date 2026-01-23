using System;

public sealed class DeviceTimeSource : ITimeSource
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public bool IsTrusted => false;
}
