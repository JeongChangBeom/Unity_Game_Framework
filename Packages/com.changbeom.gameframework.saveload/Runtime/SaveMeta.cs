namespace GameFramework.SaveLoad
{
    public static class SaveMeta
    {
        public static readonly SaveKey Root = new SaveKey("meta");
        public static readonly SaveKey SaveVersion = Root.Join("saveVersion");
        public static readonly SaveKey CreatedAtUtc = Root.Join("createdAtUtc");
        public static readonly SaveKey LastSavedAtUtc = Root.Join("lastSavedAtUtc");
    }
}
