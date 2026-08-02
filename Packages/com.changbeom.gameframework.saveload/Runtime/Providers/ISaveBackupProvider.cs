namespace GameFramework.SaveLoad
{
    public interface ISaveBackupProvider
    {
        bool HasBackup();

        /// <summary>실제로 백업 파일 쓰기까지 성공했으면 true입니다.</summary>
        bool BackupNow();

        bool RestoreFromBackup();
    }
}
