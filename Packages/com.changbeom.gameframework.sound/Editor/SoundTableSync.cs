using System;
using UnityEditor;

namespace GameFramework.SoundSystem.Editor
{
    // Data Parsing이 생성한 Sound 테이블(SoundTable)이 (재)임포트될 때마다 ESound 재생성 +
    // Addressables 등록을 자동으로 실행합니다 -- 버튼을 누를 필요가 없습니다.
    //
    // (SoundDatabaseSO를 쓰던 이전 버전과 달리) Addressables 등록이 ESound가 이미 컴파일돼
    // 있어야 한다는 조건이 없어졌으므로, 재컴파일이 끝나길 기다릴 필요 없이 두 작업을
    // 곧바로 이어서 실행합니다.
    public sealed class SoundTableSync : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject SoundTable");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (ContainsPath(importedAssets, path) || ContainsPath(movedAssets, path))
                {
                    ESoundGenerator.Generate();
                    return;
                }
            }
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
    }
}
