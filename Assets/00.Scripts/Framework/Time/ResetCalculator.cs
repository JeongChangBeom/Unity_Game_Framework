using System;

public static class ResetCalculator
{
    public static DateTimeOffset GetLastResetUtc(DateTimeOffset nowUtc, ResetRule rule)
    {
        if (rule.Type == ResetType.Daily)
        {
            return GetLastDailyResetUtc(nowUtc, rule.ResetHour);
        }

        if (rule.Type == ResetType.Weekly)
        {
            return GetLastWeeklyResetUtc(nowUtc, rule.ResetHour, rule.WeekStart);
        }

        return GetLastMonthlyResetUtc(nowUtc, rule.ResetHour);
    }

    public static DateTimeOffset GetNextResetUtc(DateTimeOffset nowUtc, ResetRule rule)
    {
        DateTimeOffset last = GetLastResetUtc(nowUtc, rule);

        if (rule.Type == ResetType.Daily)
        {
            return last.AddDays(1);
        }

        if (rule.Type == ResetType.Weekly)
        {
            return last.AddDays(7);
        }

        DateTimeOffset lastUtc = last;
        DateTime nextMonth = new DateTime(lastUtc.Year, lastUtc.Month, 1, rule.ResetHour, 0, 0, DateTimeKind.Utc).AddMonths(1);
        return new DateTimeOffset(nextMonth, TimeSpan.Zero);
    }

    public static TimeSpan GetRemainingToNextReset(DateTimeOffset nowUtc, ResetRule rule)
    {
        return GetNextResetUtc(nowUtc, rule) - nowUtc;
    }

    public static bool HasCrossedReset(DateTimeOffset lastCheckUtc, DateTimeOffset nowUtc, ResetRule rule)
    {
        DateTimeOffset a = GetLastResetUtc(lastCheckUtc, rule);
        DateTimeOffset b = GetLastResetUtc(nowUtc, rule);
        return a.UtcDateTime != b.UtcDateTime;
    }

    public static DateTimeOffset GetLastDailyResetUtc(DateTimeOffset nowUtc, int resetHour)
    {
        DateTime now = nowUtc.UtcDateTime;
        DateTime todayReset = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0, DateTimeKind.Utc);
        DateTime last = now < todayReset ? todayReset.AddDays(-1) : todayReset;
        return new DateTimeOffset(last, TimeSpan.Zero);
    }

    public static DateTimeOffset GetLastWeeklyResetUtc(DateTimeOffset nowUtc, int resetHour, DayOfWeek weekStart)
    {
        DateTime now = nowUtc.UtcDateTime;
        DateTime todayReset = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0, DateTimeKind.Utc);

        DateTime baseDay = now < todayReset ? todayReset.AddDays(-1) : todayReset;
        int diff = DaysSinceWeekStart(baseDay.DayOfWeek, weekStart);
        DateTime weekStartDay = baseDay.AddDays(-diff);

        return new DateTimeOffset(weekStartDay, TimeSpan.Zero);
    }

    public static DateTimeOffset GetLastMonthlyResetUtc(DateTimeOffset nowUtc, int resetHour)
    {
        DateTime now = nowUtc.UtcDateTime;
        DateTime monthReset = new DateTime(now.Year, now.Month, 1, resetHour, 0, 0, DateTimeKind.Utc);

        DateTime last;
        if (now < monthReset)
        {
            DateTime prev = monthReset.AddMonths(-1);
            last = new DateTime(prev.Year, prev.Month, 1, resetHour, 0, 0, DateTimeKind.Utc);
        }
        else
        {
            last = monthReset;
        }

        return new DateTimeOffset(last, TimeSpan.Zero);
    }

    private static int DaysSinceWeekStart(DayOfWeek current, DayOfWeek start)
    {
        int c = (int)current;
        int s = (int)start;

        int diff = c - s;
        if (diff < 0)
        {
            diff += 7;
        }

        return diff;
    }
}
