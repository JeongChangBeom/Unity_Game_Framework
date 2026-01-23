using System;
using System.Globalization;

public static class TimeUtil
{
    public static DateTimeOffset FromUtcTicks(long utcTicks)
    {
        if (utcTicks <= 0)
        {
            return DateTimeOffset.MinValue;
        }

        return new DateTimeOffset(utcTicks, TimeSpan.Zero);
    }

    public static long ToUtcTicks(DateTimeOffset utc)
    {
        return utc.ToUniversalTime().Ticks;
    }

    public static string FormatDaysHoursMinutes(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        int days = span.Days;
        int hours = span.Hours;
        int minutes = span.Minutes;

        return days.ToString(CultureInfo.InvariantCulture)
               + " : "
               + hours.ToString(CultureInfo.InvariantCulture)
               + " : "
               + minutes.ToString(CultureInfo.InvariantCulture);
    }

    public static string FormatHhMmSs(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        int totalHours = (int)Math.Floor(span.TotalHours);
        int minutes = span.Minutes;
        int seconds = span.Seconds;

        return totalHours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')
               + ":"
               + minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')
               + ":"
               + seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
    }
}
