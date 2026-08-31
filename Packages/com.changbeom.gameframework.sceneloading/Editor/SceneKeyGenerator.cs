using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.SceneLoading.Editor
{
    // Build Settings에 등록된 씬 목록으로 ESceneKey.cs(+ 강타입 확장 메서드)를
    // Assets/00.Scripts/GeneratedFramework/에 생성합니다.
    public static class SceneKeyGenerator
    {
        private const string OutputPath = "Assets/00.Scripts/GeneratedFramework/ESceneKey.cs";

        [MenuItem("Game Framework/Scene Loading/Generate ESceneKey From Build Settings")]
        public static void Generate()
        {
            List<string> names = new List<string>();
            HashSet<string> unique = new HashSet<string>();

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = scenes[i];

                if (scene == null || scene.enabled == false)
                {
                    continue;
                }

                string sceneName = Path.GetFileNameWithoutExtension(scene.path);

                if (!IsValidIdentifier(sceneName))
                {
                    Debug.LogError($"[SceneKeyGenerator] 유효하지 않은 씬 이름이라 건너뜁니다 (영문/숫자/밑줄만 가능, 숫자로 시작 불가): \"{sceneName}\"");
                    continue;
                }

                if (!unique.Add(sceneName))
                {
                    Debug.LogError($"[SceneKeyGenerator] 중복된 씬 이름이라 건너뜁니다 (Build Settings에서 이름이 겹치지 않게 하세요): \"{sceneName}\"");
                    continue;
                }

                names.Add(sceneName);
            }

            List<string> ordered = BuildOrderedNames(names);

            string code = BuildCode(ordered);
            WriteFile(OutputPath, code);

            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(OutputPath);
                AssetDatabase.Refresh();

                Debug.Log("[SceneKeyGenerator] 생성됨: " + OutputPath + " (개수: " + ordered.Count + ")");
            };
        }

        // 기존 멤버는 Build Settings에서 지워졌더라도 순서/값을 그대로 보존하고, 새로
        // 추가된 씬만 맨 뒤에 붙입니다(기존 ESceneKey 값이 다른 씬을 가리키지 않도록).
        private static List<string> BuildOrderedNames(List<string> currentNames)
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

            for (int i = 0; i < currentNames.Count; i++)
            {
                string name = currentNames[i];
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

                if (t.Namespace == "GameFramework.SceneLoading" && t.Name == "ESceneKey")
                {
                    return Enum.GetNames(t);
                }
            }

            return Array.Empty<string>();
        }

        private static bool IsValidIdentifier(string s)
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
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using GameFramework.SceneLoading;");
            sb.AppendLine();
            sb.AppendLine("namespace GameFramework.SceneLoading");
            sb.AppendLine("{");
            sb.AppendLine("    public enum ESceneKey");
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
            sb.AppendLine("    public static class ESceneKeyExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        public static Awaitable LoadSceneAsync(this SceneLoadingManager manager, ESceneKey sceneKey)");
            sb.AppendLine("        {");
            sb.AppendLine("            return manager.LoadSceneAsync(sceneKey, null);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static async Awaitable LoadSceneAsync(this SceneLoadingManager manager, ESceneKey sceneKey, IReadOnlyList<SceneLoadStep> extraSteps)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (sceneKey == ESceneKey.None)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError(\"[ESceneKeyExtensions] ESceneKey.None으로는 씬을 로드할 수 없습니다.\");");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            await manager.LoadSceneAsync(sceneKey.ToString(), extraSteps);");
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
    }
}
