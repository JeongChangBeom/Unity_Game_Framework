using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.SceneLoading.Editor
{
    // ESceneKey.cs는 패키지 자신의 폴더가 아니라 "이 프로젝트"의 Assets 아래에 생성됩니다.
    // git URL/레지스트리로 설치된 패키지는 읽기 전용 캐시(Library/PackageCache)에서
    // 로드되기 때문에, 패키지 자신의 Runtime 폴더에 프로젝트마다 달라지는 값을 쓰는 건
    // 애초에 성립하지 않습니다(불변/공유 콘텐츠에 프로젝트별 값을 넣으려는 모순). 그래서
    // enum은 프로젝트 쪽에 생성하고, SceneLoadingManager는 이 enum을 전혀 모르는 채로
    // string 기반 API만 제공하며, 강타입 호출부(sm.LoadSceneAsync(ESceneKey.X))는 같이
    // 생성되는 확장 메서드(ESceneKeyExtensions)로 제공합니다. Data Parsing이 ItemTable
    // 등을 프로젝트 쪽(Assets/00.Scripts/GeneratedTables)에 생성하는 것과 동일한 패턴입니다.
    //
    // 기존 멤버의 선언 순서(=정수 값)는 그대로 보존되고, 새로 등록된 씬만 맨 뒤에 추가됩니다
    // (BuildOrderedNames 참고 -- 순서가 바뀌면 기존에 저장된 ESceneKey 값이 다른 씬을
    // 가리키게 되는 사고가 생기기 때문입니다).
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
                    // 비활성화된 씬은 빌드에 포함되지 않으므로 런타임에 이름으로 로드할 수 없습니다.
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
                    // Unity는 Build Settings에 같은 이름의 씬이 여러 개 있으면 이름으로 로드할 때
                    // 어떤 씬인지 구분할 수 없어 경고만으로는 부족합니다.
                    Debug.LogError($"[SceneKeyGenerator] 중복된 씬 이름이라 건너뜁니다 (Build Settings에서 이름이 겹치지 않게 하세요): \"{sceneName}\"");
                    continue;
                }

                names.Add(sceneName);
            }

            List<string> ordered = BuildOrderedNames(names);

            string code = BuildCode(ordered);
            WriteFile(OutputPath, code);

            // OnPostprocessAllAssets 콜백(SceneFolderSync) 도중에 AssetDatabase.ImportAsset/
            // Refresh를 바로 부르면 지금 진행 중인 임포트 배치와 겹쳐 에디터가 멈출 수
            // 있으므로(Pooling/Sound/Localization과 동일한 이유), 콜백이 완전히 끝난 다음
            // 프레임으로 미룹니다.
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(OutputPath);
                AssetDatabase.Refresh();

                Debug.Log("[SceneKeyGenerator] 생성됨: " + OutputPath + " (개수: " + ordered.Count + ")");
            };
        }

        // 새로 생성할 때 이름을 알파벳순으로 정렬하면, enum은 값을 안 주면 선언 순서대로
        // 0,1,2...가 매겨지기 때문에 기존 멤버의 정수 값이 조용히 바뀌어버립니다 (코드에
        // 박아둔 ESceneKey 값이 다른 씬을 가리키게 됨). 그래서 기존 멤버는 Build Settings에서
        // 지워졌더라도 순서/값을 그대로 보존하고, 새로 추가된 씬만 맨 뒤에 붙입니다.
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

        // ESceneKey는 이제 프로젝트 쪽(Assets)에 생성되어 이 패키지가 컴파일 타임에 직접
        // 참조할 수 없으므로, 이미 컴파일된 프로젝트 어셈블리들에서 TypeCache로 찾습니다
        // (TableClassGenerator.TryFindEnumType과 동일한 패턴). 한 번도 생성된 적 없으면(첫
        // 실행) 못 찾는 게 정상이라 빈 배열을 반환합니다.
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

        // Pooling.Editor의 PoolKeyGenerator.IsValidIdentifier는 다른 어셈블리의 internal이라
        // 재사용할 수 없어, 패키지 간 에디터 툴링 결합을 만들지 않기 위해 그대로 복사합니다.
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
            sb.AppendLine("    // SceneLoadingManager는 패키지 쪽 코드라 이 프로젝트 전용 enum을 컴파일 타임에");
            sb.AppendLine("    // 알 수 없습니다. 대신 string 기반 API(LoadSceneAsync(string, ...))를 감싸는");
            sb.AppendLine("    // 확장 메서드로 강타입 호출부(sm.LoadSceneAsync(ESceneKey.X))를 그대로 제공합니다.");
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
            sb.AppendLine("                // 확장 메서드에서는 SceneLoadingManager.OnSceneLoadFailed를 직접 발행할 수");
            sb.AppendLine("                // 없어(이벤트는 선언한 클래스 밖에서 Invoke 불가), 시도 자체를 막고");
            sb.AppendLine("                // 에러만 남깁니다.");
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
