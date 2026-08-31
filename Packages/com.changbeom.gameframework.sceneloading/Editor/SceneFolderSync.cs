using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace GameFramework.SceneLoading.Editor
{
    // Assets/01.Scenes/ 폴더에 씬을 넣으면 Build Settings와 Addressables("Scene" 그룹,
    // 주소=씬 파일 이름)에 자동 등록되고, ESceneKey도 함께 재생성됩니다.
    public sealed class SceneFolderSync : AssetPostprocessor
    {
        private const string WatchFolder = "Assets/01.Scenes";
        private const string AddressablesGroupName = "Scene";

        /// <summary>이미 폴더에 있었지만 자동 감지를 못한 씬들을 한 번에 등록합니다.</summary>
        [MenuItem("Game Framework/Scene Loading/Sync Scenes From Folder")]
        public static void SyncNow()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { WatchFolder });
            string[] paths = new string[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            }

            RegisterScenes(paths);
            SceneKeyGenerator.Generate();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool buildSettingsChanged = RegisterScenes(importedAssets);
            buildSettingsChanged |= RegisterScenes(movedAssets);

            if (buildSettingsChanged)
            {
                SceneKeyGenerator.Generate();
            }
        }

        private static bool RegisterScenes(string[] paths)
        {
            bool buildSettingsChanged = false;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];

                if (!IsWatchedScene(path))
                {
                    continue;
                }

                buildSettingsChanged |= EnsureInBuildSettings(path);
                EnsureAddressable(path);
            }

            return buildSettingsChanged;
        }

        private static bool IsWatchedScene(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path.StartsWith(WatchFolder, StringComparison.OrdinalIgnoreCase)
                && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static bool EnsureInBuildSettings(string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] == null || scenes[i].guid.ToString() != guid)
                {
                    continue;
                }

                if (!scenes[i].enabled)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes;
                    Debug.Log($"[SceneFolderSync] Build Settings에서 비활성화되어 있던 씬을 다시 활성화했습니다: {path}");
                    return true;
                }

                return false;
            }

            List<EditorBuildSettingsScene> updated = new List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(path, true)
            };
            EditorBuildSettings.scenes = updated.ToArray();

            Debug.Log($"[SceneFolderSync] 새 씬을 Build Settings에 자동 등록했습니다: {path}");
            return true;
        }

        private static void EnsureAddressable(string path)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                Debug.LogWarning("[SceneFolderSync] AddressableAssetSettings를 찾지 못해 Addressables 등록을 건너뜁니다. Window/Asset Management/Addressables/Groups에서 먼저 생성하세요.");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(AddressablesGroupName);

            if (group == null)
            {
                group = settings.CreateGroup(
                    AddressablesGroupName,
                    false,
                    false,
                    true,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);

            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group);
            }
            else if (entry.parentGroup != group)
            {
                settings.MoveEntry(entry, group);
            }

            string address = Path.GetFileNameWithoutExtension(path);

            if (entry.address != address)
            {
                entry.address = address;
                EditorUtility.SetDirty(settings);
            }
        }
    }
}
