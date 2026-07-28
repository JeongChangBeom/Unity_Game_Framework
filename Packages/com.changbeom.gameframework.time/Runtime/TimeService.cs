using System;

namespace GameFramework.TimeSystem
{
    public sealed class TimeService
    {
        private readonly TimeFrameworkConfig _config;
        private readonly DeviceTimeSource _local;
        private readonly HardenedServerTimeSource _server;
        private readonly MockTimeSource _mock;

        private bool _useMock;

        public TimeService(TimeFrameworkConfig config, TimeStore store, IMonotonicClock monotonicClock)
        {
            _config = config;
            _local = new DeviceTimeSource();
            _server = new HardenedServerTimeSource(store, monotonicClock, config.ServerTrustWindowSeconds);
            _mock = new MockTimeSource(store);
        }

        public TimeMode Mode
        {
            get => _config.Mode;
            set => _config.Mode = value;
        }

        public bool UseMock
        {
            get => _useMock;
            set => _useMock = value;
        }

        public ITimeSource ActiveSource
        {
            get
            {
                if (_useMock)
                {
                    return _mock;
                }

                if (_config.Mode == TimeMode.LocalOnly)
                {
                    return _local;
                }

                if (_config.Mode == TimeMode.ServerOnly)
                {
                    return _server;
                }

                if (_server.IsTrusted)
                {
                    return _server;
                }

                return _local;
            }
        }

        public DateTimeOffset UtcNow => ActiveSource.UtcNow;
        public bool IsTrusted => ActiveSource.IsTrusted;

        public HardenedServerTimeSource Server => _server;
        public MockTimeSource Mock => _mock;
    }
}
