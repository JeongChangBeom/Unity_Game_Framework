using System;
using UnityEditor;

namespace GameFramework.Localization.Editor
{
    // LocalizationTable이 (재)임포트될 때마다 ELanguage 재생성을 자동으로 실행합니다.
    public sealed class LocalizationTableSync : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject LocalizationTable");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (ContainsPath(importedAssets, path) || ContainsPath(movedAssets, path))
                {
                    LocalizationGenerator.Generate();
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
