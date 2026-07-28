using System;

namespace GameFramework.TimeSystem
{
    public readonly struct ResetRule
    {
        public readonly ResetType Type;
        public readonly int ResetHour;
        public readonly DayOfWeek WeekStart;

        public ResetRule(ResetType type, int resetHour, DayOfWeek weekStart)
        {
            Type = type;
            ResetHour = TimeFrameworkConfig.ClampHour(resetHour);
            WeekStart = weekStart;
        }
    }
}
