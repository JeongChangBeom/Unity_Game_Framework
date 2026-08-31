using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Pooling.Editor
{
    // Pool Settings에 등록된 Key들로 EPoolKey.cs(+ 강타입 확장 메서드)를
    // Assets/00.Scripts/GeneratedFramework/에 생성합니다.
    public static class PoolKeyGenerator
    {
        private const string OutputPath = "Assets/00.Scripts/GeneratedFramework/EPoolKey.cs";

        [MenuItem("Game Framework/Pooling/Generate EPoolKey From Pool Settings")]
        public static void Generate()
        {
            PoolSettings settings = FindPoolSettings();
            if (settings == null)
            {
                Debug.LogError("[PoolKeyGenerator] PoolSettings 에셋을 찾지 못했습니다. Assets/Create/Game Framework/Pooling/Pool Settings로 먼저 생성하세요.");
                return;
            }

            List<string> names = new List<string>();
            HashSet<string> unique = new HashSet<string>();

            for (int i = 0; i < settings.entries.Count; i++)
            {
                PoolSettings.Entry entry = settings.entries[i];

                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                string key = entry.key.Trim();

                if (!IsValidIdentifier(key))
                {
                    Debug.LogError($"[PoolKeyGenerator] 유효하지 않은 Key라서 건너뜁니다 (영문/숫자/밑줄만 가능, 숫자로 시작 불가): \"{key}\"");
                    continue;
                }

                if (!unique.Add(key))
                {
                    Debug.LogWarning($"[PoolKeyGenerator] 중복된 Key라서 건너뜁니다: \"{key}\"");
                    continue;
                }

                names.Add(key);
            }

            List<string> ordered = BuildOrderedNames(names);

            string code = BuildCode(ordered);
            WriteFile(OutputPath, code);

            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(OutputPath);
                AssetDatabase.Refresh();
                Debug.Log("[PoolKeyGenerator] 생성됨: " + OutputPath + " (개수: " + ordered.Count + ")");
            };
        }

        // 기존 멤버는 Pool Settings에서 지워졌더라도 순서/값을 그대로 보존하고, 새로
        // 추가된 Key만 맨 뒤에 붙입니다(기존 EPoolKey 값이 다른 프리팹을 가리키지 않도록).
        private static List<string> BuildOrderedNames(List<string> currentKeys)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            string[] existingNames = GetExistingEnumNames();
            for (int i = 0; i < existingNames.Length; i++)
            {
                string name = existingNames[i];
                if (name == "None" || !seen.Add(name))
                {
                    continue;
                }

                result.Add(name);
            }

            for (int i = 0; i < currentKeys.Count; i++)
            {
                string name = currentKeys[i];
                if (seen.Add(name))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private static string[] GetExistingEnumNames()
        {
            TypeCache.TypeCollection candidates = TypeCache.GetTypesDerivedFrom<Enum>();

            for (int i = 0; i < candidates.Count; i++)
            {
                Type t = candidates[i];

                if (t.Namespace == "GameFramework.Pooling" && t.Name == "EPoolKey")
                {
                    return Enum.GetNames(t);
                }
            }

            return Array.Empty<string>();
        }

        internal static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            char first = s[0];
            bool firstOk =
                (first >= 'a' && first <= 'z') ||
                (first >= 'A' && first <= 'Z') ||
                first == '_';

            if (!firstOk)
            {
                return false;
            }

            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];

                bool ok =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_';

                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildCode(List<string> names)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("// 자동 생성됨. 직접 편집하지 마세요.");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using GameFramework.Pooling;");
            sb.AppendLine("using GameFramework.UISystem;");
            sb.AppendLine();
            sb.AppendLine("namespace GameFramework.Pooling");
            sb.AppendLine("{");
            sb.AppendLine("    public enum EPoolKey");
            sb.AppendLine("    {");
            sb.AppendLine("        None = 0,");

            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == "None")
                {
                    continue;
                }

                sb.Append("        ");
                sb.Append(names[i]);
                sb.AppendLine(",");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static class EPoolKeyExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        public static GameObject Spawn(this PoolManager manager, EPoolKey key, Vector3 position, Quaternion rotation, Transform parent = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (key == EPoolKey.None)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError(\"[EPoolKeyExtensions] EPoolKey.None으로는 Spawn할 수 없습니다.\");");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return manager.Spawn(key.ToString(), position, rotation, parent);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static T Spawn<T>(this PoolManager manager, EPoolKey key, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component");
            sb.AppendLine("        {");
            sb.AppendLine("            if (key == EPoolKey.None)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError(\"[EPoolKeyExtensions] EPoolKey.None으로는 Spawn할 수 없습니다.\");");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return manager.Spawn<T>(key.ToString(), position, rotation, parent);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static void RequestPopup(this UIManager uiManager, EPoolKey key, EPopupPriority priority, object payload = null, bool unique = true, EPopupPolicy policy = EPopupPolicy.PreemptIfHigher, Action<object> onResult = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            uiManager.RequestPopup(key.ToString(), priority, payload, unique, policy, onResult);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static void RequestPopup<TResult>(this UIManager uiManager, EPoolKey key, EPopupPriority priority, Action<TResult> onResult, object payload = null, bool unique = true, EPopupPolicy policy = EPopupPolicy.PreemptIfHigher)");
            sb.AppendLine("        {");
            sb.AppendLine("            uiManager.RequestPopup(key.ToString(), priority, onResult, payload, unique, policy);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void WriteFile(string path, string content)
        {
            string dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, content, new UTF8Encoding(false));
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
