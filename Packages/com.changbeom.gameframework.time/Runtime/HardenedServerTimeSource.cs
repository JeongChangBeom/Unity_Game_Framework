using System;

namespace GameFramework.TimeSystem
{
    /// <summary>
    /// Server time anchored to a monotonic (wall-clock-independent) counter, so changing the
    /// device's OS clock after sync cannot fool it. Trust expires after a configurable window
    /// since the last successful sync.
    ///
    /// The monotonic clock is OS-uptime-based, so trust correctly survives the app being
    /// closed and reopened. It does NOT survive a device reboot -- the clock resets to a
    /// small value, which is smaller than the persisted sync reading, so IsTrusted
    /// deliberately reports false until the next ApplyServerUtc. This is intentional: after
    /// a reboot there's no way to verify how much wall-clock time actually passed, so
    /// falling back to "untrusted, please resync" is the safe choice.
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
        /// How much longer the current sync stays trusted. Zero if already untrusted.
        /// Use this to decide when to proactively re-sync with the server (e.g. call
        /// ApplyServerUtc again when this drops below a few minutes).
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
