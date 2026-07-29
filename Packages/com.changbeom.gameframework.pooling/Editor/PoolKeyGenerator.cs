using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Pooling.Editor
{
    // EPoolKey.cs는 이 패키지의 Runtime 폴더 안에서 직접 재생성됩니다. PoolManager가
    // EPoolKey를 구체 타입으로 참조하기 때문에 같은 어셈블리를 공유해야 하기 때문입니다.
    // 기존 멤버의 선언 순서(=정수 값)는 그대로 보존되고, 새로 등록된 Key만 맨 뒤에 추가됩니다
    // (BuildOrderedNames 참고 -- 순서가 바뀌면 기존에 저장된 EPoolKey 값이 다른 프리팹을
    // 가리키게 되는 사고가 생기기 때문입니다).
    public static class PoolKeyGenerator
    {
        private const string OutputPath = "Packages/com.changbeom.gameframework.pooling/Runtime/EPoolKey.cs";

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

            AssetDatabase.ImportAsset(OutputPath);
            AssetDatabase.Refresh();

            Debug.Log("[PoolKeyGenerator] 생성됨: " + OutputPath + " (개수: " + ordered.Count + ")");
        }

        // 새로 생성할 때 이름을 알파벳순으로 정렬하면, enum은 값을 안 주면 선언 순서대로
        // 0,1,2...가 매겨지기 때문에 기존 멤버의 정수 값이 조용히 바뀌어버립니다 (코드에
        // 박아둔 EPoolKey 값이 다른 프리팹을 가리키게 됨). 그래서 기존 멤버는 Pool Settings에서
        // 지워졌더라도 순서/값을 그대로 보존하고, 새로 추가된 Key만 맨 뒤에 붙입니다.
        private static List<string> BuildOrderedNames(List<string> currentKeys)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            string[] existingNames = Enum.GetNames(typeof(EPoolKey));
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
