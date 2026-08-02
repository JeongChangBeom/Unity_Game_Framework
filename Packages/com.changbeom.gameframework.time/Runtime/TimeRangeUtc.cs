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

            // 시작 전에는 "활성 구간 안에서 남은 시간"이 아직 존재하지 않습니다.
            // 이전에는 여기서 전체 이벤트 길이(EndUtc - StartUtc)를 반환해,
            // IsActive를 먼저 확인하지 않고 Remaining만 바인딩하는 UI가 시작하지도
            // 않은 이벤트에 대해 그럴듯한 "남은 시간"을 표시하는 함정이 있었습니다.
            // 시작까지 남은 시간이 필요하면 UntilStart를 쓰세요.
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
