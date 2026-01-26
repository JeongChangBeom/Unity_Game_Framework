using System;

public readonly struct TimeRangeUtc
{
    public readonly DateTimeOffset StartUtc;
    public readonly DateTimeOffset EndUtc;

    public TimeRangeUtc(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc.ToUniversalTime();
        EndUtc = endUtc.ToUniversalTime();
    }

    public bool IsValid
    {
        get
        {
            return EndUtc > StartUtc;
        }
    }

    public bool IsActive(DateTimeOffset nowUtc)
    {
        if (!IsValid)
        {
            return false;
        }

        DateTimeOffset n = nowUtc.ToUniversalTime();
        return n >= StartUtc && n < EndUtc;
    }

    public TimeSpan Remaining(DateTimeOffset nowUtc)
    {
        if (!IsValid)
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset n = nowUtc.ToUniversalTime();
        if (n >= EndUtc)
        {
            return TimeSpan.Zero;
        }

        if (n < StartUtc)
        {
            return EndUtc - StartUtc;
        }

        return EndUtc - n;
    }

    public TimeSpan UntilStart(DateTimeOffset nowUtc)
    {
        if (!IsValid)
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset n = nowUtc.ToUniversalTime();
        if (n >= StartUtc)
        {
            return TimeSpan.Zero;
        }

        return StartUtc - n;
    }
}
