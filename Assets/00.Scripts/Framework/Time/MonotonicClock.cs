using System.Diagnostics;

public interface IMonotonicClock
{
    double Seconds { get; }
}

public sealed class StopwatchMonotonicClock : IMonotonicClock
{
    private static readonly double TickToSeconds = 1.0 / Stopwatch.Frequency;
    public double Seconds => Stopwatch.GetTimestamp() * TickToSeconds;
}
