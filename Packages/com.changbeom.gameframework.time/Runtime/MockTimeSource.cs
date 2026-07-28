using System;

namespace GameFramework.TimeSystem
{
    /// <summary>Test-only time source: device time plus a persisted offset the caller controls.</summary>
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
