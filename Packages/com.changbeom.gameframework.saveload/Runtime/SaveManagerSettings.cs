using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Project-specific SaveManager configuration. Create via
    /// Assets/Create/Game Framework/Save Load/Save Manager Settings and place it at
    /// Assets/Resources/GameFramework/SaveManagerSettings.asset so SaveManager can find it
    /// with no scene placement required.
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
