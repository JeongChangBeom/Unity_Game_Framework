using System;

public interface ITimeSource
{
    DateTimeOffset UtcNow { get; }
    bool IsTrusted { get; }
}
