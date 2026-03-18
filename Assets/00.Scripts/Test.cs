using System;
using UnityEngine;

public sealed class Test : MonoBehaviour
{
    [Header("Cooldown Test")]
    [SerializeField] private string _cooldownId = "skill_test_30s";
    [SerializeField] private int _cooldownSeconds = 30;

    private string _log = "";
    private Vector2 _scroll;

    private TimeManager _tm;

    private void Awake()
    {
        _tm = TimeManager.Instance;

        if (_tm == null)
        {
            Debug.LogError("TimeManager is not initialized.");
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 520, Screen.height - 40));

        GUILayout.Label("Time Tester");

        if (GUILayout.Button("1) Snapshot"))
        {
            PrintSnapshot();
        }

        GUILayout.Space(10);

        GUILayout.Label("Cooldown");

        if (GUILayout.Button("2) Try Use"))
        {
            TryUseSkill();
        }

        if (GUILayout.Button("3) Print Cooldown"))
        {
            PrintCooldown();
        }

        if (GUILayout.Button("4) Clear Cooldown"))
        {
            ClearCooldown();
        }

        GUILayout.Space(10);

        GUILayout.Label("Server");

        if (GUILayout.Button("5) Apply Server +120s"))
        {
            ApplyServerPlusSeconds(120);
        }

        if (GUILayout.Button("6) Clear Server"))
        {
            ClearServerSync();
        }

        GUILayout.Space(10);

        GUILayout.Label("Mock");

        if (GUILayout.Button("7) Enable Mock"))
        {
            EnableMock();
        }

        if (GUILayout.Button("8) Disable Mock"))
        {
            DisableMock();
        }

        if (GUILayout.Button("9) Add +60s"))
        {
            AddMockSeconds(60);
        }

        if (GUILayout.Button("10) Add -60s"))
        {
            AddMockSeconds(-60);
        }

        GUILayout.Space(10);

        GUILayout.Label("Etc");

        if (GUILayout.Button("11) Offline Delta"))
        {
            PrintOfflineDelta();
        }

        if (GUILayout.Button("12) Clear Cheat"))
        {
            ClearCheatFlag();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Clear Log"))
        {
            _log = "";
        }

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(250));
        GUILayout.TextArea(_log);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    // -------------------------
    // Safety
    // -------------------------

    private bool IsReady()
    {
        if (_tm == null)
        {
            SafeLog("TimeManager is null");
            return false;
        }

        return true;
    }

    // -------------------------
    // Snapshot
    // -------------------------

    private void PrintSnapshot()
    {
        if (!IsReady()) return;

        SafeLog("UtcNow: " + _tm.UtcNow.ToString("O"));

        // 존재하는 경우만 사용
        SafeLog("Trusted: " + _tm.IsTrusted);
        SafeLog("Cheat: " + _tm.IsCheatDetected);
    }

    // -------------------------
    // Cooldown
    // -------------------------

    private void TryUseSkill()
    {
        if (!IsReady()) return;

        bool ready = _tm.IsCooldownReady(_cooldownId);

        if (!ready)
        {
            TimeSpan remain = _tm.GetCooldownRemaining(_cooldownId);
            SafeLog("NOT READY: " + remain.TotalSeconds.ToString("0.00"));
            return;
        }

        _tm.StartCooldown(_cooldownId, TimeSpan.FromSeconds(_cooldownSeconds));
        SafeLog("USED");
    }

    private void PrintCooldown()
    {
        if (!IsReady()) return;

        bool ready = _tm.IsCooldownReady(_cooldownId);
        TimeSpan remain = _tm.GetCooldownRemaining(_cooldownId);

        SafeLog("Ready: " + ready);
        SafeLog("Remain: " + remain.TotalSeconds.ToString("0.00"));
    }

    private void ClearCooldown()
    {
        if (!IsReady()) return;

        _tm.ClearCooldown(_cooldownId);
        SafeLog("Cooldown cleared");
    }

    // -------------------------
    // Server
    // -------------------------

    private void ApplyServerPlusSeconds(int seconds)
    {
        if (!IsReady()) return;

        DateTimeOffset serverUtc = DateTimeOffset.UtcNow.AddSeconds(seconds);
        _tm.ApplyServerUtc(serverUtc);

        SafeLog("Server + " + seconds);
    }

    private void ClearServerSync()
    {
        if (!IsReady()) return;

        _tm.ClearServerSync();
        SafeLog("Server cleared");
    }

    // -------------------------
    // Mock
    // -------------------------

    private void EnableMock()
    {
        if (!IsReady()) return;

        _tm.EnableMockTime();
        SafeLog("Mock ON");
    }

    private void DisableMock()
    {
        if (!IsReady()) return;

        _tm.DisableMockTime();
        SafeLog("Mock OFF");
    }

    private void AddMockSeconds(long seconds)
    {
        if (!IsReady()) return;

        _tm.EnableMockTime();
        _tm.AddMockSeconds(seconds);

        SafeLog("Mock += " + seconds);
    }

    // -------------------------
    // Etc
    // -------------------------

    private void PrintOfflineDelta()
    {
        if (!IsReady()) return;

        TimeSpan offline = _tm.GetOfflineDelta();
        SafeLog("Offline: " + offline.TotalSeconds.ToString("0.00"));
    }

    private void ClearCheatFlag()
    {
        if (!IsReady()) return;

        _tm.ClearCheatFlag();
        SafeLog("Cheat cleared");
    }

    private void SafeLog(string msg)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
        Debug.Log(line);

        if (string.IsNullOrEmpty(_log))
        {
            _log = line;
        }
        else
        {
            _log += "\n" + line;
        }
    }
}