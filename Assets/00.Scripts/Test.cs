using System;
using UnityEngine;

public sealed class TimeFrameworkTester : MonoBehaviour
{
    [Header("Cooldown Test")]
    [SerializeField] private string _cooldownId = "skill_test_30s";
    [SerializeField] private int _cooldownSeconds = 30;

    private string _log = "";
    private Vector2 _scroll;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SafeLog("F1: Print Snapshot");
            PrintSnapshot();
        }
    }

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 14;
        GUI.skin.button.fontSize = 14;

        GUILayout.BeginArea(new Rect(20, 20, 520, Screen.height - 40));
        GUILayout.Label("Time Framework Tester (UTC)");

        if (GUILayout.Button("1) Snapshot (UtcNow / Mode / Trusted / CheatFlag)"))
        {
            PrintSnapshot();
        }

        GUILayout.Space(10);

        GUILayout.Label("Cooldown: id = " + _cooldownId + ", duration = " + _cooldownSeconds + "s");

        if (GUILayout.Button("2) Try Use Skill (Start 30s if ready)"))
        {
            TryUseSkill();
        }

        if (GUILayout.Button("3) Print Cooldown Remaining"))
        {
            PrintCooldown();
        }

        if (GUILayout.Button("4) Clear Cooldown"))
        {
            ClearCooldown();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("5) Print Reset Keys (Daily/Weekly/Monthly)"))
        {
            PrintResetKeys();
        }

        if (GUILayout.Button("6) Print Remaining To Resets"))
        {
            PrintResetRemaining();
        }

        GUILayout.Space(10);

        GUILayout.Label("Server Time");

        if (GUILayout.Button("7) Apply Server Utc = Device Utc + 120s"))
        {
            ApplyServerPlusSeconds(120);
        }

        if (GUILayout.Button("8) Clear Server Sync"))
        {
            ClearServerSync();
        }

        GUILayout.Space(10);

        GUILayout.Label("Mock Time");

        if (GUILayout.Button("9) Enable Mock"))
        {
            EnableMock();
        }

        if (GUILayout.Button("10) Disable Mock"))
        {
            DisableMock();
        }

        if (GUILayout.Button("11) Add Mock +60s"))
        {
            AddMockSeconds(60);
        }

        if (GUILayout.Button("12) Add Mock -60s (Backward Test)"))
        {
            AddMockSeconds(-60);
        }

        if (GUILayout.Button("13) Jump To Next Daily Reset (Test)"))
        {
            JumpToNextDailyReset();
        }

        GUILayout.Space(10);

        GUILayout.Label("Cheat / Offline");

        if (GUILayout.Button("14) Print Offline Delta"))
        {
            PrintOfflineDelta();
        }

        if (GUILayout.Button("15) Clear Cheat Flag"))
        {
            ClearCheatFlag();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Clear Log"))
        {
            _log = "";
        }

        GUILayout.Space(10);

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
        GUILayout.TextArea(_log);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    private bool IsReady()
    {
        TimeManager tm;

        try
        {
            tm = TimeManager.Instance;
        }
        catch (Exception e)
        {
            SafeLog("TimeManager.Instance exception: " + e.Message);
            return false;
        }

        if (tm == null)
        {
            SafeLog("TimeManager.Instance is null.");
            return false;
        }

        return true;
    }

    private void PrintSnapshot()
    {
        if (!IsReady())
        {
            return;
        }

        DateTimeOffset now = TimeManager.Instance.UtcNow;
        bool trusted = TimeManager.Instance.IsTrusted;
        TimeMode mode = TimeManager.Instance.Mode;
        bool cheat = TimeManager.Instance.IsCheatDetected;

        SafeLog("Snapshot:");
        SafeLog("  UtcNow: " + now.ToString("O"));
        SafeLog("  Mode: " + mode);
        SafeLog("  Trusted: " + trusted);
        SafeLog("  CheatDetected: " + cheat);
    }

    private void TryUseSkill()
    {
        if (!IsReady())
        {
            return;
        }

        bool ready = TimeManager.Instance.IsCooldownReady(_cooldownId);
        if (!ready)
        {
            TimeSpan remain = TimeManager.Instance.GetCooldownRemaining(_cooldownId);
            SafeLog("Skill NOT ready. Remaining: " + remain.TotalSeconds.ToString("0.00") + "s");
            return;
        }

        TimeManager.Instance.StartCooldown(_cooldownId, TimeSpan.FromSeconds(_cooldownSeconds));
        SafeLog("Skill USED. Cooldown started: " + _cooldownSeconds + "s");
    }

    private void PrintCooldown()
    {
        if (!IsReady())
        {
            return;
        }

        bool ready = TimeManager.Instance.IsCooldownReady(_cooldownId);
        TimeSpan remain = TimeManager.Instance.GetCooldownRemaining(_cooldownId);

        SafeLog("Cooldown:");
        SafeLog("  Ready: " + ready);
        SafeLog("  Remaining: " + remain.TotalSeconds.ToString("0.00") + "s");
        SafeLog("  RemainingText(HH:MM:SS): " + TimeUtil.FormatHhMmSs(remain));
    }

    private void ClearCooldown()
    {
        if (!IsReady())
        {
            return;
        }

        TimeManager.Instance.ClearCooldown("skill_test_30s");
        SafeLog("Cooldown cleared: " + _cooldownId);
    }

    private void PrintResetKeys()
    {
        if (!IsReady())
        {
            return;
        }

        int d = TimeManager.Instance.GetDailyKey();
        int w = TimeManager.Instance.GetWeeklyKey();
        int m = TimeManager.Instance.GetMonthlyKey();

        SafeLog("Reset Keys:");
        SafeLog("  DailyKey: " + d);
        SafeLog("  WeeklyKey: " + w);
        SafeLog("  MonthlyKey: " + m);
    }

    private void PrintResetRemaining()
    {
        if (!IsReady())
        {
            return;
        }

        TimeSpan d = TimeManager.Instance.GetRemainingToDailyReset();
        TimeSpan w = TimeManager.Instance.GetRemainingToWeeklyReset();
        TimeSpan m = TimeManager.Instance.GetRemainingToMonthlyReset();

        SafeLog("Remaining To Reset:");
        SafeLog("  Daily: " + d.TotalSeconds.ToString("0.00") + "s (" + TimeUtil.FormatDaysHoursMinutes(d) + ")");
        SafeLog("  Weekly: " + w.TotalSeconds.ToString("0.00") + "s (" + TimeUtil.FormatDaysHoursMinutes(w) + ")");
        SafeLog("  Monthly: " + m.TotalSeconds.ToString("0.00") + "s (" + TimeUtil.FormatDaysHoursMinutes(m) + ")");
    }

    private void ApplyServerPlusSeconds(int seconds)
    {
        if (!IsReady())
        {
            return;
        }

        DateTimeOffset serverUtc = DateTimeOffset.UtcNow.AddSeconds(seconds);
        TimeManager.Instance.ApplyServerUtc(serverUtc);

        SafeLog("ApplyServerUtc: DeviceUtc + " + seconds + "s");
        SafeLog("  Applied: " + serverUtc.ToString("O"));
        PrintSnapshot();
    }

    private void ClearServerSync()
    {
        if (!IsReady())
        {
            return;
        }

        TimeManager.Instance.ClearServerSync();
        SafeLog("Server sync cleared.");
        PrintSnapshot();
    }

    private void EnableMock()
    {
        if (!IsReady())
        {
            return;
        }

        TimeManager.Instance.EnableMockTime();
        SafeLog("Mock enabled.");
        PrintSnapshot();
    }

    private void DisableMock()
    {
        if (!IsReady())
        {
            return;
        }

        TimeManager.Instance.DisableMockTime();
        SafeLog("Mock disabled.");
        PrintSnapshot();
    }

    private void AddMockSeconds(long seconds)
    {
        if (!IsReady())
        {
            return;
        }

        TimeManager.Instance.EnableMockTime();
        TimeManager.Instance.AddMockSeconds(seconds);

        SafeLog("Mock offset changed: " + (seconds >= 0 ? "+" : "") + seconds + "s");
        PrintSnapshot();
    }

    private void JumpToNextDailyReset()
    {
        if (!IsReady())
        {
            return;
        }

        TimeManager.Instance.JumpToNextDailyResetForTest();
        SafeLog("Jumped to next daily reset (mock enabled).");
        PrintSnapshot();
        PrintResetKeys();
        PrintResetRemaining();
    }

    private void PrintOfflineDelta()
    {
        if (!IsReady())
        {
            return;
        }

        TimeSpan offline = TimeManager.Instance.GetOfflineDelta();
        SafeLog("OfflineDelta: " + offline.TotalSeconds.ToString("0.00") + "s");
    }

    private void ClearCheatFlag()
    {
        if (!IsReady())
        {
            return;
        }

        TimeManager.Instance.ClearCheatFlag();
        SafeLog("Cheat flag cleared.");
        PrintSnapshot();
    }

    private void SafeLog(string msg)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
        Debug.Log(line);

        if (string.IsNullOrEmpty(_log))
        {
            _log = line;
            return;
        }

        _log = _log + "\n" + line;
    }
}
