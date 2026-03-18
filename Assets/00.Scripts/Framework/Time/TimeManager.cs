using System;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoSingleton<TimeManager>
{
    private const string KEY_LAST_TIME = "time/last_time";
    private const string KEY_COOLDOWN = "time/cooldown";
    private const string KEY_SERVER_TIME = "time/server";
    private const string KEY_CHEAT = "time/cheat";

    private SaveManager _save;

    private DateTime _utcNow;
    private DateTime _lastTime;

    private TimeSpan _offlineDelta;

    private bool _useServer;
    private TimeSpan _serverOffset;

    private bool _useMock;
    private TimeSpan _mockOffset;

    private bool _isCheat;

    private Dictionary<string, long> _cooldowns = new Dictionary<string, long>();

    public DateTime UtcNow => GetNow();
    public bool IsTrusted => !_isCheat;
    public bool IsCheatDetected => _isCheat;

    protected override void OnInitialize()
    {
        _save = SaveManager.Instance;

        Load();
        CalculateOffline();
        DetectCheat();
    }

    private void Load()
    {
        long lastTicks = _save.GetOrDefault(KEY_LAST_TIME, 0L);

        if (lastTicks == 0)
        {
            _lastTime = DateTime.UtcNow;
        }
        else
        {
            _lastTime = new DateTime(lastTicks);
        }

        _utcNow = DateTime.UtcNow;

        _cooldowns = _save.GetOrDefault(KEY_COOLDOWN, new Dictionary<string, long>());
    }

    private void Save()
    {
        _save.Set(KEY_LAST_TIME, UtcNow.Ticks);
        _save.Set(KEY_COOLDOWN, _cooldowns);
        _save.Set(KEY_CHEAT, _isCheat);
        _save.Save();
    }
    private DateTime GetNow()
    {
        DateTime now = DateTime.UtcNow;

        if (_useServer)
        {
            now = now + _serverOffset;
        }

        if (_useMock)
        {
            now = now + _mockOffset;
        }

        return now;
    }

    private void CalculateOffline()
    {
        _offlineDelta = UtcNow - _lastTime;

        if (_offlineDelta.TotalSeconds < 0)
        {
            _offlineDelta = TimeSpan.Zero;
        }
    }

    public TimeSpan GetOfflineDelta()
    {
        return _offlineDelta;
    }

    private void DetectCheat()
    {
        if (_offlineDelta.TotalHours > 24)
        {
            _isCheat = true;
        }
    }

    public void ClearCheatFlag()
    {
        _isCheat = false;
        Save();
    }

    public bool IsCooldownReady(string id)
    {
        long endTicks;

        bool exists = _cooldowns.TryGetValue(id, out endTicks);

        if (!exists)
        {
            return true;
        }

        DateTime end = new DateTime(endTicks);

        return UtcNow >= end;
    }

    public void StartCooldown(string id, TimeSpan duration)
    {
        DateTime end = UtcNow.Add(duration);
        _cooldowns[id] = end.Ticks;
        Save();
    }

    public TimeSpan GetCooldownRemaining(string id)
    {
        long endTicks;

        bool exists = _cooldowns.TryGetValue(id, out endTicks);

        if (!exists)
        {
            return TimeSpan.Zero;
        }

        DateTime end = new DateTime(endTicks);
        TimeSpan remain = end - UtcNow;

        if (remain.TotalSeconds < 0)
        {
            return TimeSpan.Zero;
        }

        return remain;
    }

    public void ClearCooldown(string id)
    {
        if (_cooldowns.ContainsKey(id))
        {
            _cooldowns.Remove(id);
            Save();
        }
    }

    public void ApplyServerUtc(DateTimeOffset serverUtc)
    {
        DateTime local = DateTime.UtcNow;
        _serverOffset = serverUtc.UtcDateTime - local;
        _useServer = true;

        Save();
    }

    public void ClearServerSync()
    {
        _useServer = false;
        _serverOffset = TimeSpan.Zero;

        Save();
    }

    public void EnableMockTime()
    {
        _useMock = true;
    }

    public void DisableMockTime()
    {
        _useMock = false;
        _mockOffset = TimeSpan.Zero;
    }

    public void AddMockSeconds(long seconds)
    {
        _mockOffset += TimeSpan.FromSeconds(seconds);
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            Save();
        }
    }

    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();

        Save();
    }
}