using System;

namespace GameFramework.TimeSystem
{
    public enum TimeMode
    {
        LocalOnly,
        ServerOnly,
        PreferServer
    }

    public sealed class TimeFrameworkConfig
    {
        public TimeMode Mode;
        public int DailyResetHour;
        public DayOfWeek WeeklyResetDay;

        public int BackwardToleranceSeconds;
        public int ServerTrustWindowSeconds;

        public int SchemaVersion;

        public TimeFrameworkConfig(
            TimeMode mode,
            int dailyResetHour,
            DayOfWeek weeklyResetDay,
            int backwardToleranceSeconds,
            int serverTrustWindowSeconds,
            int schemaVersion
        )
        {
            Mode = mode;
            DailyResetHour = ClampHour(dailyResetHour);
            WeeklyResetDay = weeklyResetDay;

            if (backwardToleranceSeconds < 0)
            {
                backwardToleranceSeconds = 0;
            }

            if (serverTrustWindowSeconds <= 0)
            {
                serverTrustWindowSeconds = 24 * 60 * 60;
            }

            if (schemaVersion < 1)
            {
                schemaVersion = 1;
            }

            BackwardToleranceSeconds = backwardToleranceSeconds;
            ServerTrustWindowSeconds = serverTrustWindowSeconds;
            SchemaVersion = schemaVersion;
        }

        public static TimeFrameworkConfig DefaultUtc()
        {
            return new TimeFrameworkConfig(
                TimeMode.PreferServer,
                0,
                DayOfWeek.Monday,
                backwardToleranceSeconds: 120,
                serverTrustWindowSeconds: 24 * 60 * 60,
                schemaVersion: 1
            );
        }

        public static int ClampHour(int hour)
        {
            if (hour < 0)
            {
                return 0;
            }

            if (hour > 23)
            {
                return 23;
            }

            return hour;
        }
    }
}
