using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// 프로젝트별 SaveManager 설정입니다. Assets/Create/Game Framework/Save Load/Save
    /// Manager Settings로 생성한 뒤 Assets/Resources/GameFramework/SaveManagerSettings.asset
    /// 경로에 두면, 씬 배치 없이도 SaveManager가 찾을 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SaveManagerSettings",
        menuName = "Game Framework/Save Load/Save Manager Settings")]
    public sealed class SaveManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/SaveManagerSettings";

        [Header("Storage")]
        public ESaveStorageMode StorageMode = ESaveStorageMode.JsonFile;
        public string SaveFileName = "save.json";
        public string RootKey = "game";
        public int CurrentVersion = 1;

        [Header("Auto Flush")]
        public bool AutoFlushEnabled = true;
        public float AutoFlushIntervalSeconds = 5f;

        [Header("Backup")]
        public bool BackupOnPause = true;
        public bool BackupOnQuit = true;
        public bool AutoRestoreOnInit = true;
    }
}
