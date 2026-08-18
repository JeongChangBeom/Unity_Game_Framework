using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Pooling.Editor
{
    // EPoolKey.cs는 패키지 자신의 폴더가 아니라 "이 프로젝트"의 Assets 아래에 생성됩니다.
    // git URL/레지스트리로 설치된 패키지는 읽기 전용 캐시(Library/PackageCache)에서
    // 로드되기 때문에, 패키지 자신의 Runtime 폴더에 프로젝트마다 달라지는 값을 쓰는 건
    // 애초에 성립하지 않습니다(불변/공유 콘텐츠에 프로젝트별 값을 넣으려는 모순). 그래서
    // enum은 프로젝트 쪽에 생성하고, PoolManager/UIManager는 이 enum을 전혀 모르는 채로
    // string 기반 API만 제공하며, 강타입 호출부(pm.Spawn(EPoolKey.X, ...))는 같이
    // 생성되는 확장 메서드(EPoolKeyExtensions)로 제공합니다. SceneLoading의 ESceneKey와
    // 동일한 패턴입니다.
    //
    // 기존 멤버의 선언 순서(=정수 값)는 그대로 보존되고, 새로 등록된 Key만 맨 뒤에 추가됩니다
    // (BuildOrderedNames 참고 -- 순서가 바뀌면 기존에 저장된 EPoolKey 값이 다른 프리팹을
    // 가리키게 되는 사고가 생기기 때문입니다).
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

            // Generate()는 PoolFolderSync(AssetPostprocessor.OnPostprocessAllAssets) 안에서
            // 호출될 수 있습니다. 그 콜백이 아직 끝나지 않은 도중에
            // AssetDatabase.ImportAsset/Refresh를 바로 부르면, 지금 진행 중인 임포트
            // 배치와 겹치면서 에디터가 멈출 수 있습니다 (여러 프리팹을 한꺼번에 폴더에
            // 넣을 때 재현됨 - Localization 패키지에서 동일한 원인으로 이미 확인된
            // 문제입니다). 그래서 파일 쓰기까지만 동기로 하고, 실제 재임포트는 지금
            // 콜백이 완전히 끝난 다음 프레임으로 미룹니다.
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(OutputPath);
                AssetDatabase.Refresh();
                Debug.Log("[PoolKeyGenerator] 생성됨: " + OutputPath + " (개수: " + ordered.Count + ")");
            };
        }

        // 새로 생성할 때 이름을 알파벳순으로 정렬하면, enum은 값을 안 주면 선언 순서대로
        // 0,1,2...가 매겨지기 때문에 기존 멤버의 정수 값이 조용히 바뀌어버립니다 (코드에
        // 박아둔 EPoolKey 값이 다른 프리팹을 가리키게 됨). 그래서 기존 멤버는 Pool Settings에서
        // 지워졌더라도 순서/값을 그대로 보존하고, 새로 추가된 Key만 맨 뒤에 붙입니다.
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

        // EPoolKey는 이제 프로젝트 쪽(Assets)에 생성되어 이 패키지가 컴파일 타임에 직접
        // 참조할 수 없으므로, 이미 컴파일된 프로젝트 어셈블리들에서 TypeCache로 찾습니다
        // (Data Parsing의 TableClassGenerator.TryFindEnumType과 동일한 패턴). 한 번도
        // 생성된 적 없으면(첫 실행) 못 찾는 게 정상이라 빈 배열을 반환합니다.
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

        // PoolFolderSync도 폴더에서 자동 등록할 때 같은 검증을 재사용합니다.
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
            sb.AppendLine("    // PoolManager/UIManager는 패키지 쪽 코드라 이 프로젝트 전용 enum을 컴파일");
            sb.AppendLine("    // 타임에 알 수 없습니다. 대신 string 기반 API를 감싸는 확장 메서드로 강타입");
            sb.AppendLine("    // 호출부(pm.Spawn(EPoolKey.X, ...), uiManager.RequestPopup(EPoolKey.X, ...))를");
            sb.AppendLine("    // 그대로 제공합니다.");
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
