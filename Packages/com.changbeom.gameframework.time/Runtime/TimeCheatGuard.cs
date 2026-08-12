using System;

namespace GameFramework.TimeSystem
{
    /// <summary>
    /// 기기 시계가 뒤로 감을 감지합니다. 미신뢰(로컬) 소스에서의 사소한 뒤로 감은
    /// 허용되며(NTP 오차 등), 그보다 크거나 신뢰돼야 할 소스가 활성 상태일 때의
    /// 뒤로 감은 모두 치트로 표시합니다.
    /// </summary>
    public sealed class TimeCheatGuard
    {
        private readonly TimeStore _store;
        private readonly int _toleranceSeconds;

        public TimeCheatGuard(TimeStore store, int backwardToleranceSeconds)
        {
            _store = store;

            if (backwardToleranceSeconds < 0)
            {
                backwardToleranceSeconds = 0;
            }

            _toleranceSeconds = backwardToleranceSeconds;
        }

        public bool IsCheatDetected()
        {
            return _store.GetInt(TimeKeys.CheatDetected, 0) != 0;
        }

        public void ClearCheatFlag()
        {
            _store.SetInt(TimeKeys.CheatDetected, 0);
            _store.Flush();
        }

        public bool CheckBackward(DateTimeOffset nowUtc, bool isTrustedNow)
        {
            long last = _store.GetLong(TimeKeys.LastSeenUtcTicks, 0);
            long now = TimeUtil.ToUtcTicks(nowUtc);

            if (last <= 0)
            {
                _store.SetLong(TimeKeys.LastSeenUtcTicks, now);
                _store.Flush();
                return false;
            }

            if (now >= last)
            {
                _store.SetLong(TimeKeys.LastSeenUtcTicks, now);
                _store.Flush();
                return false;
            }

            long diffTicks = last - now;
            long tolTicks = (long)_toleranceSeconds * TimeSpan.TicksPerSecond;

            if (diffTicks <= tolTicks && !isTrustedNow)
            {
                // 허용 오차 이내의 사소한 뒤로 감이라 치트로는 안 잡지만, LastSeenUtcTicks
                // 기준선은 일부러 뒤로 물리지 않습니다(여기서 SetLong을 안 함). 기준선을
                // 매번 뒤로 물리면, 허용 오차 이내의 작은 되감기를 여러 번 반복해서
                // 누적으로는 오차 범위보다 훨씬 큰 폭을 몰래 되감을 수 있는 구멍이
                // 있었습니다 - 한 번에 크게 감으면 걸리지만 조금씩 여러 번 감으면 매번
                // "허용 오차 이내"로만 보였습니다. 기준선을 지금까지 본 가장 늦은
                // 시각으로 고정해두면 이 누적 우회가 막힙니다.
                return false;
            }

            _store.SetInt(TimeKeys.CheatDetected, 1);
            _store.Flush();
            return true;
        }

        public TimeSpan GetOfflineDelta(DateTimeOffset nowUtc)
        {
            long last = _store.GetLong(TimeKeys.LastSeenUtcTicks, 0);
            if (last <= 0)
            {
                return TimeSpan.Zero;
            }

            long now = TimeUtil.ToUtcTicks(nowUtc);
            long diff = now - last;

            if (diff <= 0)
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromTicks(diff);
        }
    }
}
