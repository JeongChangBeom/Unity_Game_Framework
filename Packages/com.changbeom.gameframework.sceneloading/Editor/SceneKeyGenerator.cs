using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.SceneLoading.Editor
{
    // ESceneKey.cs는 이 패키지의 Runtime 폴더 안에서 직접 재생성됩니다. SceneLoadingManager가
    // ESceneKey를 구체 타입으로 참조하기 때문에 같은 어셈블리를 공유해야 하기 때문입니다.
    // 기존 멤버의 선언 순서(=정수 값)는 그대로 보존되고, 새로 등록된 씬만 맨 뒤에 추가됩니다
    // (BuildOrderedNames 참고 -- 순서가 바뀌면 기존에 저장된 ESceneKey 값이 다른 씬을
    // 가리키게 되는 사고가 생기기 때문입니다).
    public static class SceneKeyGenerator
    {
        private const string OutputPath = "Packages/com.changbeom.gameframework.sceneloading/Runtime/ESceneKey.cs";

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

            string[] existingNames = Enum.GetNames(typeof(ESceneKey));
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
