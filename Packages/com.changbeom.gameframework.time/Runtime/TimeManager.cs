using System;
using System.Collections.Generic;
using GameFramework.Core;
using GameFramework.SaveLoad;
using UnityEngine;

namespace GameFramework.TimeSystem
{
    [BootPriority(-50)]
    public sealed class TimeManager : MonoSingleton<TimeManager>
    {
        // JsonUtility는 Dictionary를 직접 직렬화할 수 없어서, 쿨타임은 저장/로드 시점에만
        // 이 리스트 형태로 변환합니다.
        [Serializable]
        private class CooldownEntry
        {
            public string Id;
            public long EndTicks;
        }

        [Serializable]
        private class CooldownList
        {
            public List<CooldownEntry> Items = new List<CooldownEntry>();
        }

        public event Action OnDailyReset;
        public event Action OnWeeklyReset;
        public event Action OnMonthlyReset;

        private TimeManagerSettings _settings;
        private TimeStore _store;
        private TimeService _service;
        private TimeCheatGuard _cheatGuard;

        private Dictionary<string, long> _cooldowns = new Dictionary<string, long>();
        private TimeSpan _offlineDelta;

        private int _lastDailyKey;
        private int _lastWeeklyKey;
        private int _lastMonthlyKey;

        public DateTimeOffset UtcNow => _service.UtcNow;
        public bool IsTrusted => _service.IsTrusted;
        public bool IsCheatDetected => _cheatGuard.IsCheatDetected();
        public TimeMode Mode => _service.Mode;

        protected override void OnInitialize()
        {
            _settings = LoadSettings();

            SaveManager save = SaveManager.Instance;
            _store = new TimeStore(save);

            TimeFrameworkConfig config = new TimeFrameworkConfig(
                _settings.Mode,
                _settings.DailyResetHour,
                _settings.WeeklyResetDay,
                _settings.BackwardToleranceSeconds,
                _settings.ServerTrustWindowSeconds,
                _settings.SchemaVersion);

            _service = new TimeService(config, _store, new StopwatchMonotonicClock());
            _cheatGuard = new TimeCheatGuard(_store, config.BackwardToleranceSeconds);

            CheckSchemaVersion(config.SchemaVersion);
            LoadCooldowns();

            DateTimeOffset now = UtcNow;
            _offlineDelta = _cheatGuard.GetOfflineDelta(now);
            _cheatGuard.CheckBackward(now, IsTrusted);

            _lastDailyKey = ResetKey.GetDailyKey(now, _settings.DailyResetHour);
            _lastWeeklyKey = ResetKey.GetWeeklyKey(now, _settings.DailyResetHour, _settings.WeeklyResetDay);
            _lastMonthlyKey = ResetKey.GetMonthlyKey(now, _settings.DailyResetHour);

            _store.Flush();
        }

        private static TimeManagerSettings LoadSettings()
        {
            TimeManagerSettings settings = Resources.Load<TimeManagerSettings>(TimeManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[TimeManager] Resources/{TimeManagerSettings.ResourcePath}에서 TimeManagerSettings 에셋을 찾지 못했습니다. 기본값을 사용합니다. Assets/Create/Game Framework/Time System/Time Manager Settings로 에셋을 만드세요.");
            return ScriptableObject.CreateInstance<TimeManagerSettings>();
        }

        private void CheckSchemaVersion(int currentVersion)
        {
            int stored = _store.GetInt(TimeKeys.SchemaVersion, currentVersion);

            if (stored != currentVersion)
            {
                Debug.LogWarning($"[TimeManager] 저장된 Time 스키마 버전({stored})이 현재 버전({currentVersion})과 다릅니다. 필요하면 마이그레이션 로직을 추가하세요.");
                _store.SetInt(TimeKeys.SchemaVersion, currentVersion);
                _store.Flush();
            }
        }

        private void Update()
        {
            DateTimeOffset now = UtcNow;

            int dailyKey = ResetKey.GetDailyKey(now, _settings.DailyResetHour);
            if (dailyKey != _lastDailyKey)
            {
                _lastDailyKey = dailyKey;
                OnDailyReset?.Invoke();
            }

            int weeklyKey = ResetKey.GetWeeklyKey(now, _settings.DailyResetHour, _settings.WeeklyResetDay);
            if (weeklyKey != _lastWeeklyKey)
            {
                _lastWeeklyKey = weeklyKey;
                OnWeeklyReset?.Invoke();
            }

            int monthlyKey = ResetKey.GetMonthlyKey(now, _settings.DailyResetHour);
            if (monthlyKey != _lastMonthlyKey)
            {
                _lastMonthlyKey = monthlyKey;
                OnMonthlyReset?.Invoke();
            }
        }

        // ---- 모드 ----

        public void SetMode(TimeMode mode)
        {
            _service.Mode = mode;
        }

        // ---- 오프라인 / 치트 ----

        public TimeSpan GetOfflineDelta()
        {
            return _offlineDelta;
        }

        public void ClearCheatFlag()
        {
            _cheatGuard.ClearCheatFlag();
        }

        // ---- 리셋 키 ----

        public int GetDailyKey()
        {
            return ResetKey.GetDailyKey(UtcNow, _settings.DailyResetHour);
        }

        public int GetWeeklyKey()
        {
            return ResetKey.GetWeeklyKey(UtcNow, _settings.DailyResetHour, _settings.WeeklyResetDay);
        }

        public int GetMonthlyKey()
        {
            return ResetKey.GetMonthlyKey(UtcNow, _settings.DailyResetHour);
        }

        public TimeSpan GetRemainingToDailyReset()
        {
            ResetRule rule = new ResetRule(ResetType.Daily, _settings.DailyResetHour, _settings.WeeklyResetDay);
            return ResetCalculator.GetRemainingToNextReset(UtcNow, rule);
        }

        public TimeSpan GetRemainingToWeeklyReset()
        {
            ResetRule rule = new ResetRule(ResetType.Weekly, _settings.DailyResetHour, _settings.WeeklyResetDay);
            return ResetCalculator.GetRemainingToNextReset(UtcNow, rule);
        }

        public TimeSpan GetRemainingToMonthlyReset()
        {
            ResetRule rule = new ResetRule(ResetType.Monthly, _settings.DailyResetHour, _settings.WeeklyResetDay);
            return ResetCalculator.GetRemainingToNextReset(UtcNow, rule);
        }

        public string GetDailyResetRemainingText()
        {
            return TimeUtil.FormatHhMmSs(GetRemainingToDailyReset());
        }

        // ---- 서버 동기화 ----

        public void ApplyServerUtc(DateTimeOffset serverUtc)
        {
            _service.Server.ApplyServerUtc(serverUtc);
        }

        public void ClearServerSync()
        {
            _service.Server.Clear();
        }

        /// <summary>서버 동기화가 이미 미신뢰 상태이거나 withinSeconds 이내에 만료될 예정이면 true입니다 -- 재동기화 시점 판단에 사용하세요.</summary>
        public bool IsServerTrustExpiringSoon(int withinSeconds)
        {
            if (!_service.Server.IsTrusted)
            {
                return true;
            }

            return _service.Server.GetTrustRemaining().TotalSeconds <= withinSeconds;
        }

        // ---- Mock (테스트용) ----

        public void EnableMockTime()
        {
            _service.UseMock = true;
        }

        public void DisableMockTime()
        {
            _service.UseMock = false;
            _service.Mock.Clear();
        }

        public void AddMockSeconds(long seconds)
        {
            _service.Mock.AddSeconds(seconds);
        }

        public void JumpToNextDailyResetForTest()
        {
            ResetRule rule = new ResetRule(ResetType.Daily, _settings.DailyResetHour, _settings.WeeklyResetDay);
            TimeSpan remain = ResetCalculator.GetRemainingToNextReset(UtcNow, rule);

            EnableMockTime();
            AddMockSeconds((long)remain.TotalSeconds + 1);
        }

        // ---- 쿨타임 ----

        public bool IsCooldownReady(string id)
        {
            ValidateCooldownId(id);

            if (!_cooldowns.TryGetValue(id, out long endTicks))
            {
                return true;
            }

            return TimeUtil.ToUtcTicks(UtcNow) >= endTicks;
        }

        public void StartCooldown(string id, TimeSpan duration)
        {
            ValidateCooldownId(id);

            DateTimeOffset end = UtcNow.Add(duration);
            _cooldowns[id] = TimeUtil.ToUtcTicks(end);
            SaveCooldowns();
        }

        public TimeSpan GetCooldownRemaining(string id)
        {
            ValidateCooldownId(id);

            if (!_cooldowns.TryGetValue(id, out long endTicks))
            {
                return TimeSpan.Zero;
            }

            long remainTicks = endTicks - TimeUtil.ToUtcTicks(UtcNow);
            return remainTicks > 0 ? TimeSpan.FromTicks(remainTicks) : TimeSpan.Zero;
        }

        public void ClearCooldown(string id)
        {
            ValidateCooldownId(id);

            if (_cooldowns.Remove(id))
            {
                SaveCooldowns();
            }
        }

        private static void ValidateCooldownId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Cooldown id is null or whitespace.", nameof(id));
            }
        }

        /// <summary>아직 끝나지 않은 모든 쿨타임을 id 기준으로 반환합니다 -- UI 목록 표시에 유용합니다.</summary>
        public IReadOnlyDictionary<string, TimeSpan> GetAllCooldownsRemaining()
        {
            Dictionary<string, TimeSpan> result = new Dictionary<string, TimeSpan>();
            long nowTicks = TimeUtil.ToUtcTicks(UtcNow);

            foreach (KeyValuePair<string, long> kvp in _cooldowns)
            {
                long remainTicks = kvp.Value - nowTicks;
                if (remainTicks > 0)
                {
                    result[kvp.Key] = TimeSpan.FromTicks(remainTicks);
                }
            }

            return result;
        }

        private void LoadCooldowns()
        {
            CooldownList list = _store.LoadOrCreate(TimeKeys.Cooldowns, () => new CooldownList(), saveIfMissing: true);

            _cooldowns = new Dictionary<string, long>();
            foreach (CooldownEntry entry in list.Items)
            {
                _cooldowns[entry.Id] = entry.EndTicks;
            }
        }

        private void SaveCooldowns()
        {
            CooldownList list = new CooldownList();
            foreach (KeyValuePair<string, long> kvp in _cooldowns)
            {
                list.Items.Add(new CooldownEntry { Id = kvp.Key, EndTicks = kvp.Value });
            }

            _store.Save(TimeKeys.Cooldowns, list);
            _store.Flush();
        }

        // ---- 라이프사이클 ----

        private void OnApplicationPause(bool pause)
        {
            DateTimeOffset now = UtcNow;

            if (pause)
            {
                _cheatGuard.CheckBackward(now, IsTrusted);
                return;
            }

            _cheatGuard.CheckBackward(now, IsTrusted);
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            _cheatGuard.CheckBackward(UtcNow, IsTrusted);
        }
    }
}
