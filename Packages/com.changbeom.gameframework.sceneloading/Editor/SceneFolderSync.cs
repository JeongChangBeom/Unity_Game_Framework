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
    // Assets/01.Scenes/ 폴더에 씬을 넣기만 하면 Build Settings와 Addressables("Scene" 그룹,
    // 주소=씬 파일 이름)에 자동 등록되고, Build Settings가 바뀌었으면 곧바로 ESceneKey도
    // 재생성됩니다 -- 버튼을 누를 필요가 없습니다. Pooling의 PoolFolderSync와 동일한 패턴입니다.
    //
    // Build Settings 등록은 이미 있으면 건드리지 않지만(중복 추가 방지), Addressables 등록은
    // ESoundGenerator처럼 매번 그룹/주소를 최신 상태로 맞춰줍니다 -- 씬 파일 이름이 나중에
    // 바뀌어도 주소가 그대로 따라가게 하기 위함입니다(Addressables의 흔한 함정 중 하나가
    // 에셋을 리네임해도 주소는 그대로 안 바뀌는 것입니다).
    public sealed class SceneFolderSync : AssetPostprocessor
    {
        private const string WatchFolder = "Assets/01.Scenes";
        private const string AddressablesGroupName = "Scene";

        // 이미 폴더에 있었지만 한 번도 (재)임포트된 적이 없어서 자동 감지를 못한 씬들을 한
        // 번에 몰아서 등록하고 싶을 때 쓰는 수동 보조 명령입니다. 평소 워크플로우에서는
        // 필요 없습니다 (새 씬을 넣으면 자동으로 등록되므로).
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

                // 이미 등록은 되어 있습니다. Assets/01.Scenes/에 있는 씬은 항상 빌드에
                // 포함되는 게 이 자동화의 목적이므로, 비활성화되어 있으면 다시 켭니다
                // (경로 자체는 Unity가 GUID 기준으로 알아서 최신 상태로 유지함).
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
