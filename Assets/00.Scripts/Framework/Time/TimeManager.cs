using System;
using System.Collections.Generic;

public sealed class TimeManager
{
    private readonly TimeFrameworkConfig _config;
    private readonly TimeStore _store;

    private readonly TimeService _time;
    private readonly TimeCheatGuard _guard;

    private readonly ResetRule _dailyRule;
    private readonly ResetRule _weeklyRule;
    private readonly ResetRule _monthlyRule;

    private readonly Dictionary<string, CooldownTimer> _cooldowns = new Dictionary<string, CooldownTimer>(64);

    public TimeManager(TimeFrameworkConfig config, SaveManager save, IMonotonicClock monotonicClock)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (save == null)
        {
            throw new ArgumentNullException(nameof(save));
        }

        if (monotonicClock == null)
        {
            throw new ArgumentNullException(nameof(monotonicClock));
        }

        _config = config;
        _store = new TimeStore(save);

        EnsureSchema(config.SchemaVersion);

        _time = new TimeService(config, _store, monotonicClock);
        _guard = new TimeCheatGuard(_store, config.BackwardToleranceSeconds);

        _dailyRule = new ResetRule(ResetType.Daily, config.DailyResetHour, config.WeeklyResetDay);
        _weeklyRule = new ResetRule(ResetType.Weekly, config.DailyResetHour, config.WeeklyResetDay);
        _monthlyRule = new ResetRule(ResetType.Monthly, config.DailyResetHour, config.WeeklyResetDay);
    }

    public DateTimeOffset UtcNow => _time.UtcNow;
    public bool IsTrusted => _time.IsTrusted;

    public TimeMode Mode
    {
        get => _time.Mode;
        set => _time.Mode = value;
    }

    public void ApplyServerUtc(DateTimeOffset serverUtc)
    {
        _time.Server.ApplyServerUtc(serverUtc);
    }

    public void ClearServerSync()
    {
        _time.Server.Clear();
    }

    public bool IsCheatDetected => _guard.IsCheatDetected();

    public void ClearCheatFlag()
    {
        _guard.ClearCheatFlag();
    }

    public void OnAppPause()
    {
        _guard.RecordLastSeen(UtcNow);
    }

    public bool OnAppResume()
    {
        return _guard.CheckBackward(UtcNow, IsTrusted);
    }

    public TimeSpan GetOfflineDelta()
    {
        return _guard.GetOfflineDelta(UtcNow);
    }

    public TimeSpan GetRemainingToDailyReset()
    {
        return ResetCalculator.GetRemainingToNextReset(UtcNow, _dailyRule);
    }

    public TimeSpan GetRemainingToWeeklyReset()
    {
        return ResetCalculator.GetRemainingToNextReset(UtcNow, _weeklyRule);
    }

    public TimeSpan GetRemainingToMonthlyReset()
    {
        return ResetCalculator.GetRemainingToNextReset(UtcNow, _monthlyRule);
    }

    public string GetDailyResetRemainingText()
    {
        return TimeUtil.FormatDaysHoursMinutes(GetRemainingToDailyReset());
    }

    public int GetDailyKey()
    {
        return ResetKey.GetDailyKey(UtcNow, _dailyRule.ResetHour);
    }

    public int GetWeeklyKey()
    {
        return ResetKey.GetWeeklyKey(UtcNow, _weeklyRule.ResetHour, _weeklyRule.WeekStart);
    }

    public int GetMonthlyKey()
    {
        return ResetKey.GetMonthlyKey(UtcNow, _monthlyRule.ResetHour);
    }

    public void StartCooldown(string id, TimeSpan duration)
    {
        CooldownTimer cd = GetOrCreateCooldown(id);
        cd.Start(UtcNow, duration);
    }

    public bool IsCooldownReady(string id)
    {
        CooldownTimer cd = GetOrCreateCooldown(id);
        return cd.IsReady(UtcNow);
    }

    public TimeSpan GetCooldownRemaining(string id)
    {
        CooldownTimer cd = GetOrCreateCooldown(id);
        return cd.GetRemaining(UtcNow);
    }

    public void ClearCooldown(string id)
    {
        CooldownTimer cd = GetOrCreateCooldown(id);
        cd.Clear();
    }

    public void EnableMockTime()
    {
        _time.UseMock = true;
    }

    public void DisableMockTime()
    {
        _time.UseMock = false;
    }

    public void AddMockSeconds(long seconds)
    {
        _time.Mock.AddSeconds(seconds);
    }

    public void JumpToNextDailyResetForTest()
    {
        DateTimeOffset nowUtc = UtcNow;
        DateTime now = nowUtc.UtcDateTime;

        int hour = _config.DailyResetHour;
        DateTime todayReset = new DateTime(now.Year, now.Month, now.Day, hour, 0, 0, DateTimeKind.Utc);

        DateTime targetUtc;
        if (now < todayReset)
        {
            targetUtc = todayReset.AddMinutes(1);
        }
        else
        {
            targetUtc = todayReset.AddDays(1).AddMinutes(1);
        }

        DateTimeOffset baseUtc = DateTimeOffset.UtcNow;
        DateTimeOffset target = new DateTimeOffset(targetUtc, TimeSpan.Zero);

        TimeSpan delta = target - baseUtc;
        long seconds = (long)Math.Round(delta.TotalSeconds);

        _time.Mock.SetOffsetSeconds(seconds);
        _time.UseMock = true;
    }

    private CooldownTimer GetOrCreateCooldown(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Cooldown id is null or whitespace.", nameof(id));
        }

        CooldownTimer cd;
        bool exists = _cooldowns.TryGetValue(id, out cd);
        if (exists)
        {
            return cd;
        }

        cd = new CooldownTimer(_store, id);
        _cooldowns.Add(id, cd);
        return cd;
    }

    private void EnsureSchema(int currentSchemaVersion)
    {
        int saved = _store.GetInt(TimeKeys.SchemaVersion, 0);

        if (saved == 0)
        {
            _store.SetInt(TimeKeys.SchemaVersion, currentSchemaVersion);
            _store.Flush();
            return;
        }

        if (saved == currentSchemaVersion)
        {
            return;
        }

        RunMigrations(saved, currentSchemaVersion);

        _store.SetInt(TimeKeys.SchemaVersion, currentSchemaVersion);
        _store.Flush();
    }

    private void RunMigrations(int from, int to)
    {
        if (from < 1)
        {
            from = 1;
        }

        if (to < from)
        {
            return;
        }
    }
}
