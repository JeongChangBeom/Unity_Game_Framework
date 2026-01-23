public static class TimeKeys
{
    public const string SchemaVersion = "Meta.SchemaVersion";

    public const string ServerSyncServerUtcTicks = "Server.ServerUtcAtSyncTicks";
    public const string ServerSyncMonotonicSeconds = "Server.MonotonicSecondsAtSync";
    public const string ServerSyncDeviceUtcTicks = "Server.DeviceUtcAtSyncTicks";

    public const string LastSeenUtcTicks = "Guard.LastSeenUtcTicks";
    public const string CheatDetected = "Guard.CheatDetected";

    public const string MockOffsetSeconds = "Debug.MockOffsetSeconds";
}
