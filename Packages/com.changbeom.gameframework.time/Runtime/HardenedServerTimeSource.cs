using System;

namespace GameFramework.TimeSystem
{
    /// <summary>
    /// 모노토닉(실제 시계와 무관한) 카운터에 앵커링된 서버 시간입니다. 동기화 이후
    /// 기기의 OS 시계를 바꿔도 속일 수 없습니다. 마지막 동기화 후 설정된 유효 기간이
    /// 지나거나 기기가 재부팅되면 신뢰가 만료되어 IsTrusted가 false가 됩니다 - 재동기화가
    /// 필요합니다(ApplyServerUtc).
    /// </summary>
    public sealed class HardenedServerTimeSource : ITimeSource
    {
        private readonly TimeStore _store;
        private readonly IMonotonicClock _mono;
        private readonly int _trustWindowSeconds;

        private long _serverUtcAtSyncTicks;
        private double _monoAtSyncSeconds;
        private long _deviceUtcAtSyncTicks;

        private double _maxObservedMonoSeconds;
        private long _maxObservedWallClockUtcTicks;

        private const double RebootDetectionSlackSeconds = 300.0;

        public HardenedServerTimeSource(TimeStore store, IMonotonicClock monotonicClock, int trustWindowSeconds)
        {
            _store = store;
            _mono = monotonicClock;
            _trustWindowSeconds = trustWindowSeconds;

            _serverUtcAtSyncTicks = store.GetLong(TimeKeys.ServerSyncServerUtcTicks, 0);
            _monoAtSyncSeconds = ReadDouble(store, TimeKeys.ServerSyncMonotonicSeconds, 0.0);
            _deviceUtcAtSyncTicks = store.GetLong(TimeKeys.ServerSyncDeviceUtcTicks, 0);
            _maxObservedMonoSeconds = ReadDouble(store, TimeKeys.ServerSyncMaxObservedMonotonicSeconds, 0.0);
            _maxObservedWallClockUtcTicks = store.GetLong(TimeKeys.ServerSyncMaxObservedWallClockUtcTicks, 0);
        }

        // 모노토닉 클럭이 이전 최댓값보다 작아지면 재부팅이 일어났다는 뜻입니다. 최댓값
        // 관측 시점의 벽시계도 같이 저장해서, 재부팅 없이 정상적으로 흐른 시간과 실제
        // 재부팅을 구분합니다.
        private bool CheckAndUpdateRebootGuard()
        {
            double nowMono = _mono.Seconds;
            long nowWallTicks = TimeUtil.ToUtcTicks(DateTimeOffset.UtcNow);

            if (nowMono < _maxObservedMonoSeconds)
            {
                Clear();
                return false;
            }

            if (_maxObservedWallClockUtcTicks > 0)
            {
                double elapsedWallSeconds = (nowWallTicks - _maxObservedWallClockUtcTicks) / (double)TimeSpan.TicksPerSecond;

                if (elapsedWallSeconds > 0.0)
                {
                    double expectedMinMono = _maxObservedMonoSeconds + elapsedWallSeconds - RebootDetectionSlackSeconds;

                    if (nowMono < expectedMinMono)
                    {
                        Clear();
                        return false;
                    }
                }
            }

            if (nowMono > _maxObservedMonoSeconds)
            {
                _maxObservedMonoSeconds = nowMono;
                WriteDouble(_store, TimeKeys.ServerSyncMaxObservedMonotonicSeconds, _maxObservedMonoSeconds);
            }

            _maxObservedWallClockUtcTicks = nowWallTicks;
            _store.SetLong(TimeKeys.ServerSyncMaxObservedWallClockUtcTicks, _maxObservedWallClockUtcTicks);

            return true;
        }

        public DateTimeOffset UtcNow
        {
            get
            {
                if (!IsTrusted)
                {
                    return DateTimeOffset.UtcNow;
                }

                double nowMono = _mono.Seconds;
                double delta = nowMono - _monoAtSyncSeconds;

                if (delta < 0.0)
                {
                    return DateTimeOffset.UtcNow;
                }

                long deltaTicks = (long)Math.Round(delta * TimeSpan.TicksPerSecond);
                long ticks = _serverUtcAtSyncTicks + deltaTicks;

                if (ticks <= 0)
                {
                    return DateTimeOffset.UtcNow;
                }

                return new DateTimeOffset(ticks, TimeSpan.Zero);
            }
        }

        public bool IsTrusted
        {
            get
            {
                if (!CheckAndUpdateRebootGuard())
                {
                    return false;
                }

                if (_serverUtcAtSyncTicks <= 0)
                {
                    return false;
                }

                if (_monoAtSyncSeconds <= 0.0)
                {
                    return false;
                }

                if (_deviceUtcAtSyncTicks <= 0)
                {
                    return false;
                }

                double nowMono = _mono.Seconds;
                if (nowMono <= 0.0)
                {
                    return false;
                }

                if (nowMono < _monoAtSyncSeconds)
                {
                    return false;
                }

                double ageSeconds = nowMono - _monoAtSyncSeconds;
                if (ageSeconds > _trustWindowSeconds)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// 현재 동기화가 앞으로 얼마나 더 신뢰되는지 나타냅니다. 이미 미신뢰 상태면 0입니다.
        /// 서버와 선제적으로 재동기화할 시점을 판단하는 데 사용하세요 (예: 이 값이 몇 분
        /// 이하로 떨어지면 ApplyServerUtc를 다시 호출).
        /// </summary>
        public TimeSpan GetTrustRemaining()
        {
            if (!IsTrusted)
            {
                return TimeSpan.Zero;
            }

            double ageSeconds = _mono.Seconds - _monoAtSyncSeconds;
            double remainingSeconds = _trustWindowSeconds - ageSeconds;

            return remainingSeconds > 0 ? TimeSpan.FromSeconds(remainingSeconds) : TimeSpan.Zero;
        }

        public void ApplyServerUtc(DateTimeOffset serverUtc)
        {
            DateTimeOffset su = serverUtc.ToUniversalTime();

            _serverUtcAtSyncTicks = TimeUtil.ToUtcTicks(su);
            _monoAtSyncSeconds = _mono.Seconds;
            _deviceUtcAtSyncTicks = TimeUtil.ToUtcTicks(DateTimeOffset.UtcNow);

            _store.SetLong(TimeKeys.ServerSyncServerUtcTicks, _serverUtcAtSyncTicks);
            WriteDouble(_store, TimeKeys.ServerSyncMonotonicSeconds, _monoAtSyncSeconds);
            _store.SetLong(TimeKeys.ServerSyncDeviceUtcTicks, _deviceUtcAtSyncTicks);
            _store.Flush();
        }

        public void Clear()
        {
            _serverUtcAtSyncTicks = 0;
            _monoAtSyncSeconds = 0.0;
            _deviceUtcAtSyncTicks = 0;

            _store.SetLong(TimeKeys.ServerSyncServerUtcTicks, 0);
            WriteDouble(_store, TimeKeys.ServerSyncMonotonicSeconds, 0.0);
            _store.SetLong(TimeKeys.ServerSyncDeviceUtcTicks, 0);
            _store.Flush();
        }

        private static double ReadDouble(TimeStore s, string key, double defaultValue)
        {
            string raw = s.GetString(key, "");
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            double v;
            bool ok = double.TryParse(raw, out v);
            if (!ok)
            {
                return defaultValue;
            }

            return v;
        }

        private static void WriteDouble(TimeStore s, string key, double value)
        {
            s.SetString(key, value.ToString("R"));
        }
    }
}
