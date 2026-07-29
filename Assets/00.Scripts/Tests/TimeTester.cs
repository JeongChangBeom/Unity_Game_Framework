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

        private void HandleDailyReset() => Log("이벤트: OnDailyReset 발생");
        private void HandleWeeklyReset() => Log("이벤트: OnWeeklyReset 발생");
        private void HandleMonthlyReset() => Log("이벤트: OnMonthlyReset 발생");

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
                    Log("사용됨");
                }
                else
                {
                    Log("아직 준비 안 됨: " + tm.GetCooldownRemaining(_cooldownId).TotalSeconds.ToString("0.00"));
                }
            }

            if (GUILayout.Button("3) Clear Cooldown"))
            {
                TimeManager.Instance.ClearCooldown(_cooldownId);
                Log("쿨타임 초기화됨");
            }

            if (GUILayout.Button("4) List All Active Cooldowns"))
            {
                var all = TimeManager.Instance.GetAllCooldownsRemaining();
                if (all.Count == 0)
                {
                    Log("활성 쿨타임 없음");
                }
                else
                {
                    foreach (var kvp in all)
                    {
                        Log($"  {kvp.Key}: {kvp.Value.TotalSeconds:0.00}초 남음");
                    }
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Server Sync");
            if (GUILayout.Button("5) Apply Server +120s"))
            {
                TimeManager.Instance.ApplyServerUtc(DateTimeOffset.UtcNow.AddSeconds(120));
                Log("서버 시간 +120초 적용됨");
            }

            if (GUILayout.Button("6) Clear Server Sync"))
            {
                TimeManager.Instance.ClearServerSync();
                Log("서버 동기화 해제됨");
            }

            if (GUILayout.Button("7) Is Trust Expiring Soon (within 60s)?"))
            {
                Log("만료 임박: " + TimeManager.Instance.IsServerTrustExpiringSoon(60));
            }

            GUILayout.Space(10);
            GUILayout.Label("Mock");
            if (GUILayout.Button("8) Enable Mock"))
            {
                TimeManager.Instance.EnableMockTime();
                Log("Mock 켜짐");
            }

            if (GUILayout.Button("9) Disable Mock"))
            {
                TimeManager.Instance.DisableMockTime();
                Log("Mock 꺼짐");
            }

            if (GUILayout.Button("10) Add +60s"))
            {
                TimeManager.Instance.AddMockSeconds(60);
                Log("Mock +60초");
            }

            if (GUILayout.Button("11) Jump To Next Daily Reset"))
            {
                TimeManager.Instance.JumpToNextDailyResetForTest();
                Log("다음 일일 리셋 시점을 넘겼습니다 (위에서 OnDailyReset 발생 확인)");
            }

            GUILayout.Space(10);
            GUILayout.Label("Reset Keys / Offline / Cheat");
            if (GUILayout.Button("12) Print Reset Keys + Remaining"))
            {
                TimeManager tm = TimeManager.Instance;
                Log($"Daily={tm.GetDailyKey()} ({tm.GetDailyResetRemainingText()} 남음), Weekly={tm.GetWeeklyKey()}, Monthly={tm.GetMonthlyKey()}");
            }

            if (GUILayout.Button("13) Offline Delta"))
            {
                Log("오프라인 경과: " + TimeManager.Instance.GetOfflineDelta().TotalSeconds.ToString("0.00") + "초");
            }

            if (GUILayout.Button("14) Clear Cheat Flag"))
            {
                TimeManager.Instance.ClearCheatFlag();
                Log("치트 플래그 초기화됨");
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
