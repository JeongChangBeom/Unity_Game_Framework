using System;

namespace GameFramework.TimeSystem
{
    /// <summary>가공하지 않은 기기 시계입니다. 사용자가 OS 시계를 바꿀 수 있어 이것만으로는 절대 신뢰하지 않습니다.</summary>
    public sealed class DeviceTimeSource : ITimeSource
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public bool IsTrusted => false;
    }
}
