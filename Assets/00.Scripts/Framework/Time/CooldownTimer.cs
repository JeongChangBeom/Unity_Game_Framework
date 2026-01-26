using System;

public sealed class CooldownTimer
{
    private readonly TimeStore _store;
    private readonly string _endKey;

    public CooldownTimer(TimeStore store, string uniqueId)
    {
        _store = store;
        _endKey = "Cooldown.EndUtcTicks." + uniqueId;
    }

    public void Start(DateTimeOffset nowUtc, TimeSpan duration)
    {
        DateTimeOffset endUtc = nowUtc.Add(duration);
        _store.SetLong(_endKey, TimeUtil.ToUtcTicks(endUtc));
        _store.Flush();
    }

    public void Clear()
    {
        _store.SetLong(_endKey, 0);
        _store.Flush();
    }

    public bool IsReady(DateTimeOffset nowUtc)
    {
        long end = _store.GetLong(_endKey, 0);
        if (end <= 0)
        {
            return true;
        }

        long nowTicks = TimeUtil.ToUtcTicks(nowUtc);
        return nowTicks >= end;
    }

    public TimeSpan GetRemaining(DateTimeOffset nowUtc)
    {
        long end = _store.GetLong(_endKey, 0);
        if (end <= 0)
        {
            return TimeSpan.Zero;
        }

        long nowTicks = TimeUtil.ToUtcTicks(nowUtc);
        long diff = end - nowTicks;

        if (diff <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(diff);
    }
}
