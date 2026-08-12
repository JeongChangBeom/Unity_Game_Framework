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

        // 지금까지 관측한 모노토닉 클럭의 최댓값과, 그걸 관측한 시점의 기기 벽시계입니다.
        // 재부팅 감지에 씁니다 - 아래 CheckAndUpdateRebootGuard 참고.
        private double _maxObservedMonoSeconds;
        private long _maxObservedWallClockUtcTicks;

        // 재부팅 감지에서 실제 재부팅과 정상적인 시계 오차(NTP 보정, 절전 관련 미세한
        // 클럭 오차 등)를 구분하기 위한 여유입니다. 너무 작으면 오탐이 나고, 너무 크면
        // 감지가 둔감해집니다.
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

        // 모노토닉 클럭이 이전에 한 번이라도 관측했던 최댓값보다 작아지면, 그건 재부팅이
        // 일어났다는 확실한 증거입니다(Stopwatch 기반 클럭은 재부팅 전까지는 절대
        // 거꾸로 가지 않으므로). 하지만 이 비교만으로는 부족합니다: 재부팅 후 기기
        // 가동 시간이 다시 그 예전 최댓값을 자연스럽게 넘어서는 순간부터는(예: 재부팅
        // 직후 앱을 안 열고 기기를 오래 켜둔 뒤 나중에 여는 경우) 신뢰가 잘못 되살아납니다
        // -- 최댓값 자체는 "넘어섰다"는 사실만 볼 뿐 그 사이에 재부팅이 있었는지는 모르기
        // 때문입니다.
        //
        // 그래서 마지막으로 관측했을 때의 벽시계도 같이 저장해두고, "재부팅이 없었다면
        // 모노토닉 클럭도 그때 이후 흐른 벽시계 시간만큼 늘어나 있어야 한다"는 기댓값과
        // 비교합니다. 재부팅이 있었다면 새 세션의 모노토닉 클럭은 실제 재부팅 이후
        // 가동 시간일 뿐이라, 마지막 관측 시점부터 지금까지의 전체 경과 시간(기댓값)보다
        // 항상 작을 수밖에 없습니다(재부팅이 마지막 관측 이후에 일어났으므로). 벽시계
        // 자체는 조작 가능하지만, 이 검사가 막으려는 것은 "정상적으로 시간이 흘러
        // 최댓값을 넘어서는" 케이스이지 벽시계 조작이 아니므로 문제되지 않습니다 -
        // 벽시계를 조작해 기댓값을 속이는 시나리오는 원래 있던 단순 최댓값 비교로도
        // 못 막던 것과 동일한 수준입니다.
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

                // 클래스 설명에서 약속한 대로, 만료 판정도 반드시 모노토닉 클럭 기준으로
                // 계산해야 합니다. 기기 벽시계(DateTimeOffset.UtcNow) 기준으로 계산하면
                // 동기화 이후 시계가 앞으로 조정될 때 신뢰가 조기 만료되고, 뒤로 조정되면
                // 오히려 즉시 미신뢰 처리되어(아래 ageSeconds < 0) 이 클래스가 막으려는
                // 바로 그 시계 조작에 그대로 휘둘리게 됩니다.
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
