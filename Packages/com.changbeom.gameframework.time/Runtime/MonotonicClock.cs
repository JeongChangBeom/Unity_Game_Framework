using System.Diagnostics;

namespace GameFramework.TimeSystem
{
    public interface IMonotonicClock
    {
        double Seconds { get; }
    }

    /// <summary>
    /// Wall-clock-independent timer (System.Diagnostics.Stopwatch), used to anchor server
    /// time trust so that changing the device's OS clock can't fool it.
    /// </summary>
    public sealed class StopwatchMonotonicClock : IMonotonicClock
    {
        private static readonly double TickToSeconds = 1.0 / Stopwatch.Frequency;
        public double Seconds => Stopwatch.GetTimestamp() * TickToSeconds;
    }
}
