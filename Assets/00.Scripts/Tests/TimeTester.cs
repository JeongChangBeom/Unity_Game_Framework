using System;
using GameFramework.TimeSystem;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class TimeTester : MonoBehaviour
    {
        [SerializeField] private string _cooldownId = "skill_test_30s";
        [SerializeField] private int _cooldownSeconds = 30;

        private string _log = "";
        private Vector2 _scroll;

        private void OnEnable()
        {
            TimeManager.Instance.OnDailyReset += HandleDailyReset;
            TimeManager.Instance.OnWeeklyReset += HandleWeeklyReset;
            TimeManager.Instance.OnMonthlyReset += HandleMonthlyReset;
        }

        private void OnDisable()
        {
            if (TimeManager.Instance == null)
            {
                return;
            }

            TimeManager.Instance.OnDailyReset -= HandleDailyReset;
            TimeManager.Instance.OnWeeklyReset -= HandleWeeklyReset;
            TimeManager.Instance.OnMonthlyReset -= HandleMonthlyReset;
        }

        private void HandleDailyReset() => Log("EVENT: OnDailyReset fired");
        private void HandleWeeklyReset() => Log("EVENT: OnWeeklyReset fired");
        private void HandleMonthlyReset() => Log("EVENT: OnMonthlyReset fired");

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("Time Tester");

            GUILayout.Label("Snapshot");
            if (GUILayout.Button("1) Snapshot"))
            {
                TimeManager tm = TimeManager.Instance;
                Log($"UtcNow={tm.UtcNow:O}, Mode={tm.Mode}, Trusted={tm.IsTrusted}, Cheat={tm.IsCheatDetected}");
            }

            GUILayout.Space(10);
            GUILayout.Label("Cooldown");
            if (GUILayout.Button("2) Try Use"))
            {
                TimeManager tm = TimeManager.Instance;
                if (tm.IsCooldownReady(_cooldownId))
                {
                    tm.StartCooldown(_cooldownId, TimeSpan.FromSeconds(_cooldownSeconds));
                    Log("USED");
                }
                else
                {
                    Log("NOT READY: " + tm.GetCooldownRemaining(_cooldownId).TotalSeconds.ToString("0.00"));
                }
            }

            if (GUILayout.Button("3) Clear Cooldown"))
            {
                TimeManager.Instance.ClearCooldown(_cooldownId);
                Log("Cooldown cleared");
            }

            if (GUILayout.Button("4) List All Active Cooldowns"))
            {
                var all = TimeManager.Instance.GetAllCooldownsRemaining();
                if (all.Count == 0)
                {
                    Log("No active cooldowns");
                }
                else
                {
                    foreach (var kvp in all)
                    {
                        Log($"  {kvp.Key}: {kvp.Value.TotalSeconds:0.00}s left");
                    }
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Server Sync");
            if (GUILayout.Button("5) Apply Server +120s"))
            {
                TimeManager.Instance.ApplyServerUtc(DateTimeOffset.UtcNow.AddSeconds(120));
                Log("Server applied +120s");
            }

            if (GUILayout.Button("6) Clear Server Sync"))
            {
                TimeManager.Instance.ClearServerSync();
                Log("Server sync cleared");
            }

            if (GUILayout.Button("7) Is Trust Expiring Soon (within 60s)?"))
            {
                Log("ExpiringSoon: " + TimeManager.Instance.IsServerTrustExpiringSoon(60));
            }

            GUILayout.Space(10);
            GUILayout.Label("Mock");
            if (GUILayout.Button("8) Enable Mock"))
            {
                TimeManager.Instance.EnableMockTime();
                Log("Mock ON");
            }

            if (GUILayout.Button("9) Disable Mock"))
            {
                TimeManager.Instance.DisableMockTime();
                Log("Mock OFF");
            }

            if (GUILayout.Button("10) Add +60s"))
            {
                TimeManager.Instance.AddMockSeconds(60);
                Log("Mock += 60s");
            }

            if (GUILayout.Button("11) Jump To Next Daily Reset"))
            {
                TimeManager.Instance.JumpToNextDailyResetForTest();
                Log("Jumped past next daily reset (watch for OnDailyReset above)");
            }

            GUILayout.Space(10);
            GUILayout.Label("Reset Keys / Offline / Cheat");
            if (GUILayout.Button("12) Print Reset Keys + Remaining"))
            {
                TimeManager tm = TimeManager.Instance;
                Log($"Daily={tm.GetDailyKey()} ({tm.GetDailyResetRemainingText()} left), Weekly={tm.GetWeeklyKey()}, Monthly={tm.GetMonthlyKey()}");
            }

            if (GUILayout.Button("13) Offline Delta"))
            {
                Log("Offline: " + TimeManager.Instance.GetOfflineDelta().TotalSeconds.ToString("0.00") + "s");
            }

            if (GUILayout.Button("14) Clear Cheat Flag"))
            {
                TimeManager.Instance.ClearCheatFlag();
                Log("Cheat flag cleared");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Clear Log"))
            {
                _log = "";
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
            GUILayout.TextArea(_log);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
