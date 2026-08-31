using System;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Pooling.Editor
{
    // Assets/02.Prefabs/Pooling/ 폴더에 프리팹을 넣으면 PoolSettings에 자동 등록되고
    // (Key=프리팹 이름, Prewarm=1, Max=1, AutoExpand=true), EPoolKey도 함께 재생성됩니다.
    public sealed class PoolFolderSync : AssetPostprocessor
    {
        private const string WatchFolder = "Assets/02.Prefabs/Pooling";

        /// <summary>이미 폴더에 있었지만 자동 감지를 못한 프리팹들을 한 번에 등록합니다.</summary>
        [MenuItem("Game Framework/Pooling/Sync Pool Settings From Folder")]
        public static void SyncNow()
        {
            PoolSettings settings = FindPoolSettings();

            if (settings == null)
            {
                Debug.LogError("[PoolFolderSync] PoolSettings 에셋을 찾지 못했습니다. Assets/Create/Game Framework/Pooling/Pool Settings로 먼저 생성하세요.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { WatchFolder });
            string[] paths = new string[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            }

            bool changed = AddNewPrefabs(settings, paths);
            ApplyIfChanged(settings, changed, forceRegenerateKey: true);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            PoolSettings settings = FindPoolSettings();

            if (settings == null)
            {
                return;
            }

            bool entriesChanged = AddNewPrefabs(settings, importedAssets);
            entriesChanged |= AddNewPrefabs(settings, movedAssets);

            string settingsPath = AssetDatabase.GetAssetPath(settings);
            bool settingsAssetTouched = ContainsPath(importedAssets, settingsPath)
                || ContainsPath(movedAssets, settingsPath);

            ApplyIfChanged(settings, entriesChanged, forceRegenerateKey: settingsAssetTouched);
        }

        private static void ApplyIfChanged(PoolSettings settings, bool entriesChanged, bool forceRegenerateKey)
        {
            if (entriesChanged)
            {
                EditorUtility.SetDirty(settings);
                EditorApplication.delayCall += AssetDatabase.SaveAssets;
            }

            if (entriesChanged || forceRegenerateKey)
            {
                PoolKeyGenerator.Generate();
            }
        }

        private static bool AddNewPrefabs(PoolSettings settings, string[] paths)
        {
            bool changed = false;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];

                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (!path.StartsWith(WatchFolder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null || IsAlreadyRegistered(settings, prefab))
                {
                    continue;
                }

                string key = ResolveKey(settings, prefab.name);

                PoolSettings.Entry entry = new PoolSettings.Entry();
                entry.key = key;
                entry.prefab = prefab;
                entry.prewarmCount = 1;
                entry.maxCount = 1;
                entry.autoExpand = true;

                settings.entries.Add(entry);
                changed = true;

                Debug.Log($"[PoolFolderSync] 새 프리팹을 PoolSettings에 자동 등록했습니다: {prefab.name} (폴더: {WatchFolder})");
            }

            return changed;
        }

        private static string ResolveKey(PoolSettings settings, string prefabName)
        {
            if (!PoolKeyGenerator.IsValidIdentifier(prefabName))
            {
                Debug.LogWarning($"[PoolFolderSync] \"{prefabName}\"은(는) 유효한 Key 이름이 아니라서(영문/숫자/밑줄만, 숫자로 시작 불가) Key 없이 등록합니다. Key로 스폰하려면 Inspector에서 직접 지정하세요.");
                return null;
            }

            for (int i = 0; i < settings.entries.Count; i++)
            {
                if (settings.entries[i] != null && string.Equals(settings.entries[i].key, prefabName, StringComparison.Ordinal))
                {
                    Debug.LogWarning($"[PoolFolderSync] \"{prefabName}\" Key가 이미 다른 프리팹에 등록되어 있어 Key 없이 등록합니다. Inspector에서 직접 다른 Key를 지정하세요.");
                    return null;
                }
            }

            return prefabName;
        }

        private static bool IsAlreadyRegistered(PoolSettings settings, GameObject prefab)
        {
            for (int i = 0; i < settings.entries.Count; i++)
            {
                if (settings.entries[i] != null && settings.entries[i].prefab == prefab)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPath(string[] paths, string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return false;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                if (string.Equals(paths[i], target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static PoolSettings FindPoolSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:PoolSettings");

            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<PoolSettings>(path);
        }
    }
}
