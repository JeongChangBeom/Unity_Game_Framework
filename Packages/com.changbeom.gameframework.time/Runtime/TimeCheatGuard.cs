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
