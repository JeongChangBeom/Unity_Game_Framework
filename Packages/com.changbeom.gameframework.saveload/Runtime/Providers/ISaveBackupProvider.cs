namespace GameFramework.SaveLoad
{
    public interface ISaveBackupProvider
    {
        bool HasBackup();
        void BackupNow();
        bool RestoreFromBackup();
    }
}
