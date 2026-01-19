public interface ISaveBackupProvider
{
    bool HasBackup();
    bool BackupNow();
    bool RestoreFromBackup();
}
