using System.Diagnostics;

namespace GameFramework.TimeSystem
{
    public interface IMonotonicClock
    {
        double Seconds { get; }
    }

    /// <summary>
    /// 실제 시계와 무관한 타이머(System.Diagnostics.Stopwatch)로, 서버 시간 신뢰를
    /// 앵커링해서 기기의 OS 시계를 바꿔도 속일 수 없게 만드는 데 사용합니다.
    /// </summary>
    public sealed class StopwatchMonotonicClock : IMonotonicClock
    {
        private static readonly double TickToSeconds = 1.0 / Stopwatch.Frequency;
        public double Seconds => Stopwatch.GetTimestamp() * TickToSeconds;
    }
}
