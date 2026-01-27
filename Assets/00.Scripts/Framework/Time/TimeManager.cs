using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TimeManager : MonoSingleton<TimeManager>
{
    public DateTimeOffset UtcNow
    {
        get
        {
            EnsureReady();
            return _time.UtcNow;
        }
    }

    public bool IsTrusted
    {
        get
        {
            EnsureReady();
            return _time.IsTrusted;
        }
    }

    public TimeMode Mode
    {
        get
        {
            EnsureReady();
            return _time.Mode;
        }
        set
        {
            EnsureReady();
            _time.Mode = value;
        }
    }

    public bool IsCheatDetected
    {
        get
        {
            EnsureReady();
            return _guard.IsCheatDetected();
        }
    }

    [Header("Config (UTC)")]
    [SerializeField] private TimeMode _mode = TimeMode.PreferServer;
    [SerializeField] private int _dailyResetHour = 0;
    [SerializeField] private DayOfWeek _weeklyResetDay = DayOfWeek.Monday;

    [Header("Security")]
    [SerializeField] private int _backwardToleranceSeconds = 120;

    [Header("Server Time Trust Window")]
    [SerializeField] private int _serverTrustWindowSeconds = 24 * 60 * 60;

    [Header("Schema")]
    [SerializeField] private int _schemaVersion = 1;

    private bool _ready;

    private TimeFrameworkConfig _config;
    private TimeStore _store;

    private TimeService _time;
    private TimeCheatGuard _guard;

    private ResetRule _dailyRule;
    private ResetRule _weeklyRule;
    private ResetRule _monthlyRule;

    private IMonotonicClock _mono;

    private readonly Dictionary<string, CooldownTimer> _cooldowns = new Dictionary<string, CooldownTimer>(64);

    protected override void OnInitialize()
    {
        if (_ready)
        {
            return;
        }

        if (SaveManager.Instance == null || !SaveManager.Instance.IsInitialized)
        {
            return;
        }

        _config = new TimeFrameworkConfig(
            _mode,
            _dailyResetHour,
            _weeklyResetDay,
            _backwardToleranceSeconds,
            _serverTrustWindowSeconds,
            _schemaVersion
        );

        _mono = new StopwatchMonotonicClock();
        _store = new TimeStore(SaveManager.Instance);

        EnsureSchema(_config.SchemaVersion);

        _time = new TimeService(_config, _store, _mono);
        _guard = new TimeCheatGuard(_store, _config.BackwardToleranceSeconds);

        _dailyRule = new ResetRule(ResetType.Daily, _config.DailyResetHour, _config.WeeklyResetDay);
        _weeklyRule = new ResetRule(ResetType.Weekly, _config.DailyResetHour, _config.WeeklyResetDay);
        _monthlyRule = new ResetRule(ResetType.Monthly, _config.DailyResetHour, _config.WeeklyResetDay);

        _ready = true;
    }

    private void OnApplicationPause(bool pause)
    {
        if (!_ready)
        {
            return;
        }

        if (pause)
        {
            _guard.RecordLastSeen(_time.UtcNow);
        }
        else
        {
            _guard.CheckBackward(_time.UtcNow, _time.IsTrusted);
        }
    }

    public void ApplyServerUtc(DateTimeOffset serverUtc)
    {
        EnsureReady();
        _time.Server.ApplyServerUtc(serverUtc);
    }

    public void ClearServerSync()
    {
        EnsureReady();
        _time.Server.Clear();
    }

    public void ClearCheatFlag()
    {
        EnsureReady();
        _guard.ClearCheatFlag();
    }

    public TimeSpan GetOfflineDelta()
    {
        EnsureReady();
        return _guard.GetOfflineDelta(_time.UtcNow);
    }

    public TimeSpan GetRemainingToDailyReset()
    {
        EnsureReady();
        return ResetCalculator.GetRemainingToNextReset(_time.UtcNow, _dailyRule);
    }

    public TimeSpan GetRemainingToWeeklyReset()
    {
        EnsureReady();
        return ResetCalculator.GetRemainingToNextReset(_time.UtcNow, _weeklyRule);
    }

    public TimeSpan GetRemainingToMonthlyReset()
    {
        EnsureReady();
        return ResetCalculator.GetRemainingToNextReset(_time.UtcNow, _monthlyRule);
    }

    public string GetDailyResetRemainingText()
    {
        EnsureReady();
        return TimeUtil.FormatDaysHoursMinutes(GetRemainingToDailyReset());
    }

    public int GetDailyKey()
    {
        EnsureReady();
        return ResetKey.GetDailyKey(_time.UtcNow, _dailyRule.ResetHour);
    }

    public int GetWeeklyKey()
    {
        EnsureReady();
        return ResetKey.GetWeeklyKey(_time.UtcNow, _weeklyRule.ResetHour, _weeklyRule.WeekStart);
    }

    public int GetMonthlyKey()
    {
        EnsureReady();
        return ResetKey.GetMonthlyKey(_time.UtcNow, _monthlyRule.ResetHour);
    }

    public void StartCooldown(string id, TimeSpan duration)
    {
        EnsureReady();
        CooldownTimer cd = GetOrCreateCooldown(id);
        cd.Start(_time.UtcNow, duration);
    }

    public bool IsCooldownReady(string id)
    {
        EnsureReady();
        CooldownTimer cd = GetOrCreateCooldown(id);
        return cd.IsReady(_time.UtcNow);
    }

    public TimeSpan GetCooldownRemaining(string id)
    {
        EnsureReady();
        CooldownTimer cd = GetOrCreateCooldown(id);
        return cd.GetRemaining(_time.UtcNow);
    }

    public void ClearCooldown(string id)
    {
        EnsureReady();
        CooldownTimer cd = GetOrCreateCooldown(id);
        cd.Clear();
    }

    public void EnableMockTime()
    {
        EnsureReady();
        _time.UseMock = true;
    }

    public void DisableMockTime()
    {
        EnsureReady();
        _time.UseMock = false;
    }

    public void AddMockSeconds(long seconds)
    {
        EnsureReady();
        _time.Mock.AddSeconds(seconds);
    }

    public void JumpToNextDailyResetForTest()
    {
        EnsureReady();

        DateTimeOffset nowUtc = _time.UtcNow;
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

    private void EnsureReady()
    {
        if (!_ready)
        {
            throw new InvalidOperationException("TimeManager is not initialized yet. Ensure SaveManager is initialized before TimeManager.");
        }
    }
}
