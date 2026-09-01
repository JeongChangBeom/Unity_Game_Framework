using UnityEditor;
using UnityEngine;

namespace GameFramework.SaveLoad.Editor
{
    public static class SaveDataMenu
    {
        [MenuItem("Game Framework/Save Load/Delete All Data")]
        public static void DeleteAllData()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "저장 데이터 초기화",
                "이 프로젝트가 저장한 모든 데이터(자동/수동 백업 포함)를 삭제합니다.\n" +
                "PlayerPrefs 저장 방식에서는 이 프로젝트의 PlayerPrefs 전체가 삭제됩니다.\n" +
                "되돌릴 수 없습니다. 계속하시겠습니까?",
                "초기화",
                "취소");

            if (!confirmed)
            {
                return;
            }

            // Play 모드에서 이미 SaveManager가 떠 있으면 그 인스턴스를 통해 지웁니다 - 그래야
            // SaveManager가 메모리에 들고 있는 캐시도 함께 비워집니다. 여기서 새 Provider를
            // 따로 만들어 파일만 지우면, 다음 AutoFlush 때 SaveManager가 메모리에 남아있던
            // 옛 데이터를 다시 그대로 파일에 써버려 방금 지운 게 무의미해집니다.
            if (Application.isPlaying && SaveManager.Instance != null)
            {
                SaveManager.Instance.DeleteAllData();
                Debug.Log("[SaveDataMenu] 모든 저장 데이터를 초기화했습니다 (SaveManager.Instance).");
                return;
            }

            SaveManagerSettings settings = Resources.Load<SaveManagerSettings>(SaveManagerSettings.ResourcePath);
            if (settings == null)
            {
                Debug.LogWarning($"[SaveDataMenu] Resources/{SaveManagerSettings.ResourcePath}에서 SaveManagerSettings 에셋을 찾지 못했습니다. 기본값(JsonFile, save.json)으로 삭제를 진행합니다.");
                settings = ScriptableObject.CreateInstance<SaveManagerSettings>();
            }

            ISaveProvider provider = CreateProvider(settings);
            provider.DeleteAll();

            Debug.Log("[SaveDataMenu] 모든 저장 데이터를 초기화했습니다.");
        }

        private static ISaveProvider CreateProvider(SaveManagerSettings settings)
        {
            switch (settings.StorageMode)
            {
                case ESaveStorageMode.PlayerPrefs:
                    return new PlayerPrefsSaveProvider();

                case ESaveStorageMode.Memory:
                    return new MemorySaveProvider();

                case ESaveStorageMode.Es3:
#if USE_ES3
                    return new ES3SaveProvider(settings.SaveFileName);
#else
                    return new JsonFileSaveProvider(settings.SaveFileName, settings.AutoRestoreOnInit);
#endif

                default:
                    return new JsonFileSaveProvider(settings.SaveFileName, settings.AutoRestoreOnInit);
            }
        }
    }
}
