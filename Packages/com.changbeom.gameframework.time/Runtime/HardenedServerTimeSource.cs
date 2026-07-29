using System;

namespace GameFramework.TimeSystem
{
    /// <summary>
    /// 모노토닉(실제 시계와 무관한) 카운터에 앵커링된 서버 시간입니다. 동기화 이후
    /// 기기의 OS 시계를 바꿔도 속일 수 없습니다. 마지막으로 동기화에 성공한 뒤 설정된
    /// 유효 기간이 지나면 신뢰가 만료됩니다.
    ///
    /// 모노토닉 클럭은 OS 부팅 이후 누적 시간 기준이라, 앱을 껐다 켜도 신뢰 상태가
    /// 올바르게 유지됩니다. 다만 기기 재부팅에는 살아남지 못합니다 -- 클럭이 작은 값으로
    /// 리셋되어 저장된 동기화 값보다 작아지므로, 다음 ApplyServerUtc 전까지 IsTrusted가
    /// 의도적으로 false를 반환합니다. 이는 의도된 동작입니다: 재부팅 후에는 실제로
    /// 얼마만큼의 시간이 지났는지 검증할 방법이 없으므로, "미신뢰, 재동기화 필요" 상태로
    /// 안전하게 대체하는 것입니다.
    /// </summary>
    public sealed class HardenedServerTimeSource : ITimeSource
    {
        private readonly TimeStore _store;
        private readonly IMonotonicClock _mono;
        private readonly int _trustWindowSeconds;

        private long _serverUtcAtSyncTicks;
        private double _monoAtSyncSeconds;
        private long _deviceUtcAtSyncTicks;

        public HardenedServerTimeSource(TimeStore store, IMonotonicClock monotonicClock, int trustWindowSeconds)
        {
            _store = store;
            _mono = monotonicClock;
            _trustWindowSeconds = trustWindowSeconds;

            _serverUtcAtSyncTicks = store.GetLong(TimeKeys.ServerSyncServerUtcTicks, 0);
            _monoAtSyncSeconds = ReadDouble(store, TimeKeys.ServerSyncMonotonicSeconds, 0.0);
            _deviceUtcAtSyncTicks = store.GetLong(TimeKeys.ServerSyncDeviceUtcTicks, 0);
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

                DateTimeOffset deviceNow = DateTimeOffset.UtcNow;
                long ageTicks = TimeUtil.ToUtcTicks(deviceNow) - _deviceUtcAtSyncTicks;

                if (ageTicks < 0)
                {
                    return false;
                }

                long limitTicks = (long)_trustWindowSeconds * TimeSpan.TicksPerSecond;
                if (ageTicks > limitTicks)
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

            DateTimeOffset deviceNow = DateTimeOffset.UtcNow;
            long ageTicks = TimeUtil.ToUtcTicks(deviceNow) - _deviceUtcAtSyncTicks;
            long limitTicks = (long)_trustWindowSeconds * TimeSpan.TicksPerSecond;
            long remainingTicks = limitTicks - ageTicks;

            return remainingTicks > 0 ? TimeSpan.FromTicks(remainingTicks) : TimeSpan.Zero;
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
