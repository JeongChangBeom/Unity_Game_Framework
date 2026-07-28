using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Data.Editor
{
    [InitializeOnLoad]
    public static class DataTableAssetBuilder
    {
        private const string PendingKey = "GameFrameworkData_PendingCreate";
        private static bool _scheduled;

        static DataTableAssetBuilder()
        {
            EditorApplication.delayCall += TryCreatePendingAssets;
        }

        public static void SetPending(List<string> payloadLines)
        {
            string joined = string.Join("\n", payloadLines);

            // SessionState (not EditorPrefs) because this is throwaway data that only
            // needs to survive the domain reload triggered by newly-generated scripts
            // recompiling -- EditorPrefs would persist it globally across every project
            // on the machine, which is the wrong scope for this.
            SessionState.SetString(PendingKey, joined);

            if (!_scheduled)
            {
                _scheduled = true;
                EditorApplication.delayCall += TryCreatePendingAssets;
            }
        }

        private static void TryCreatePendingAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryCreatePendingAssets;
                return;
            }

            _scheduled = false;

            string joined = SessionState.GetString(PendingKey, "");
            SessionState.EraseString(PendingKey);

            if (string.IsNullOrEmpty(joined))
            {
                return;
            }

            string[] lines = joined.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('|');
                if (parts.Length < 3)
                {
                    continue;
                }

                string className = parts[0];
                string assetPath = parts[1];
                string escapedTsv = parts[2];
                string tsv = escapedTsv.Replace("\\n", "\n");

                Type t = FindScriptableObjectTypeByName(className);
                if (t == null)
                {
                    Debug.LogError("[GameFramework.Data] Type not found: " + className);
                    continue;
                }

                ScriptableObject asset = AssetDatabase.LoadAssetAtPath(assetPath, t) as ScriptableObject;
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance(t);
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                var method = t.GetMethod("ParseFromTsv");
                method?.Invoke(asset, new object[] { tsv });
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Type FindScriptableObjectTypeByName(string className)
        {
            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>();

            Type found = null;

            for (int i = 0; i < types.Count; i++)
            {
                Type t = types[i];

                if (t.Name != className)
                {
                    continue;
                }

                if (found != null)
                {
                    Debug.LogError("[GameFramework.Data] 동일 이름 ScriptableObject 타입이 여러 개입니다: " + className);
                    return null;
                }

                found = t;
            }

            return found;
        }
    }
}
