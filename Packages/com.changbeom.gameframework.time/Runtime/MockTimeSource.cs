using System;

namespace GameFramework.TimeSystem
{
    /// <summary>테스트 전용 시간 소스: 기기 시간에 호출하는 쪽이 제어하는, 영구 저장되는 오프셋을 더한 값입니다.</summary>
    public sealed class MockTimeSource : ITimeSource
    {
        private readonly TimeStore _store;

        public MockTimeSource(TimeStore store)
        {
            _store = store;
        }

        public DateTimeOffset UtcNow
        {
            get
            {
                long offsetSeconds = _store.GetLong(TimeKeys.MockOffsetSeconds, 0);
                return DateTimeOffset.UtcNow.AddSeconds(offsetSeconds);
            }
        }

        public bool IsTrusted => true;

        public void SetOffsetSeconds(long offsetSeconds)
        {
            _store.SetLong(TimeKeys.MockOffsetSeconds, offsetSeconds);
            _store.Flush();
        }

        public void AddSeconds(long deltaSeconds)
        {
            long cur = _store.GetLong(TimeKeys.MockOffsetSeconds, 0);
            _store.SetLong(TimeKeys.MockOffsetSeconds, cur + deltaSeconds);
            _store.Flush();
        }

        public void Clear()
        {
            _store.SetLong(TimeKeys.MockOffsetSeconds, 0);
            _store.Flush();
        }
    }
}
