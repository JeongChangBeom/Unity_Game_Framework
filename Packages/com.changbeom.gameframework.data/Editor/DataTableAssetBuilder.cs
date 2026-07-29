using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameFramework.DataParsing.Editor
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

            // EditorPrefs가 아니라 SessionState를 쓰는 이유: 이 데이터는 새로 생성된
            // 스크립트가 재컴파일되면서 발생하는 domain reload만 버티면 되는 일회성
            // 데이터이기 때문입니다 -- EditorPrefs는 이 머신의 모든 프로젝트에 걸쳐
            // 전역으로 유지되므로, 여기에는 범위가 맞지 않습니다.
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
                    Debug.LogError("[GameFramework.DataParsing] 타입을 찾지 못했습니다: " + className);
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
                    Debug.LogError("[GameFramework.DataParsing] 동일 이름 ScriptableObject 타입이 여러 개입니다: " + className);
                    return null;
                }

                found = t;
            }

            return found;
        }
    }
}
