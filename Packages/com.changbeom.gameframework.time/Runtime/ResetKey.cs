using System;

namespace GameFramework.TimeSystem
{
    public static class ResetKey
    {
        public static int GetDailyKey(DateTimeOffset nowUtc, int resetHour)
        {
            DateTimeOffset last = ResetCalculator.GetLastDailyResetUtc(nowUtc, resetHour);
            DateTime d = last.UtcDateTime;
            return (d.Year * 10000) + (d.Month * 100) + d.Day;
        }

        public static int GetMonthlyKey(DateTimeOffset nowUtc, int resetHour)
        {
            DateTimeOffset last = ResetCalculator.GetLastMonthlyResetUtc(nowUtc, resetHour);
            DateTime d = last.UtcDateTime;
            return (d.Year * 100) + d.Month;
        }

        public static int GetWeeklyKey(DateTimeOffset nowUtc, int resetHour, DayOfWeek weekStart)
        {
            DateTimeOffset last = ResetCalculator.GetLastWeeklyResetUtc(nowUtc, resetHour, weekStart);
            DateTime d = last.UtcDateTime;
            return (d.Year * 10000) + (d.Month * 100) + d.Day;
        }
    }
}
