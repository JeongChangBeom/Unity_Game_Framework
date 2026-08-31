using System;

namespace GameFramework.TimeSystem
{
    /// <summary>시작/종료 UTC 구간입니다. 예: 기간 한정 인게임 이벤트.</summary>
    public readonly struct TimeRangeUtc
    {
        public readonly DateTimeOffset StartUtc;
        public readonly DateTimeOffset EndUtc;

        public TimeRangeUtc(DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            StartUtc = startUtc.ToUniversalTime();
            EndUtc = endUtc.ToUniversalTime();
        }

        public bool IsValid => EndUtc > StartUtc;

        public bool IsActive(DateTimeOffset nowUtc)
        {
            if (!IsValid)
            {
                return false;
            }

            DateTimeOffset n = nowUtc.ToUniversalTime();
            return n >= StartUtc && n < EndUtc;
        }

        public TimeSpan Remaining(DateTimeOffset nowUtc)
        {
            if (!IsValid)
            {
                return TimeSpan.Zero;
            }

            DateTimeOffset n = nowUtc.ToUniversalTime();
            if (n >= EndUtc)
            {
                return TimeSpan.Zero;
            }

            // 시작 전에는 0을 반환합니다 - 시작까지 남은 시간이 필요하면 UntilStart를 쓰세요.
            if (n < StartUtc)
            {
                return TimeSpan.Zero;
            }

            return EndUtc - n;
        }

        public TimeSpan UntilStart(DateTimeOffset nowUtc)
        {
            if (!IsValid)
            {
                return TimeSpan.Zero;
            }

            DateTimeOffset n = nowUtc.ToUniversalTime();
            if (n >= StartUtc)
            {
                return TimeSpan.Zero;
            }

            return StartUtc - n;
        }
    }
}
