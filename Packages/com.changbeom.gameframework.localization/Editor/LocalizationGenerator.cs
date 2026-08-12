using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Localization.Editor
{
    // ELocKey.cs/ELanguage.cs는 (Data Parsing이 생성하는 프로젝트 쪽 폴더가 아니라) 이
    // 패키지의 Runtime 폴더 안에서 직접 재생성됩니다. LocalizationManager가 이 두
    // enum을 구체 타입으로 참조하기 때문에 같은 어셈블리를 공유해야 하기 때문입니다.
    //
    // Data Parsing이 생성한 Localization 테이블은 프로젝트 쪽에 있어서 이 패키지가
    // 타입으로 직접 참조할 수 없기 때문에, 리플렉션(SerializedProperty)으로 KeyName과
    // 언어 컬럼명만 읽어옵니다. LocalizationManager는 런타임에 같은 테이블을
    // System.Reflection으로 다시 읽어 텍스트 데이터를 자체 캐시로 만듭니다
    // (LocalizationManager.cs 참고).
    public static class LocalizationGenerator
    {
        private const string LocKeyOutputPath = "Packages/com.changbeom.gameframework.localization/Runtime/ELocKey.cs";
        private const string LanguageOutputPath = "Packages/com.changbeom.gameframework.localization/Runtime/ELanguage.cs";

        [MenuItem("Game Framework/Localization/Generate ELocKey + ELanguage From Localization Table")]
        public static void Generate()
        {
            ScriptableObject table = FindLocalizationTableSo();
            if (table == null)
            {
                Debug.LogError("Localization 테이블 SO를 찾지 못했습니다. Data Parsing으로 Localization 시트를 먼저 생성하세요 (예: 탭 이름 Localization -> 클래스 LocalizationTable).");
                return;
            }

            SerializedObject so = new SerializedObject(table);
            SerializedProperty tableProp = FindTableProperty(so);

            if (tableProp == null)
            {
                Debug.LogError("Localization 테이블에서 데이터 배열을 찾지 못했습니다.");
                return;
            }

            if (tableProp.arraySize == 0)
            {
                Debug.LogError("Localization 테이블에 행이 없습니다. 최소 1행 이상 데이터를 채운 뒤 다시 생성하세요 (언어 컬럼은 행 데이터에서 감지합니다).");
                return;
            }

            List<string> keyNames = ExtractKeyNames(tableProp);
            List<string> languageColumns = ExtractLanguageColumns(tableProp.GetArrayElementAtIndex(0));

            if (keyNames.Count == 0)
            {
                Debug.LogError("Localization 테이블에서 KeyName 값을 찾지 못했습니다.");
                return;
            }

            if (languageColumns.Count == 0)
            {
                Debug.LogError("Localization 테이블에서 언어 컬럼을 찾지 못했습니다 (RowKey/KeyName을 제외한 string 컬럼이 없습니다).");
                return;
            }

            List<string> orderedLocKeys = BuildOrderedNames(keyNames, Enum.GetNames(typeof(ELocKey)));
            List<string> orderedLanguages = BuildOrderedNames(languageColumns, Enum.GetNames(typeof(ELanguage)));

            WriteFile(LocKeyOutputPath, BuildCode("ELocKey", orderedLocKeys));
            WriteFile(LanguageOutputPath, BuildCode("ELanguage", orderedLanguages));

            // Generate()는 LocalizationTableSync(AssetPostprocessor.OnPostprocessAllAssets)
            // 안에서 호출될 수 있습니다. 그 콜백이 아직 끝나지 않은 도중에
            // AssetDatabase.ImportAsset/Refresh를 바로 부르면, 지금 진행 중인 임포트
            // 배치와 겹치면서 에디터가 멈출 수 있습니다(여러 시트를 한꺼번에 "선택 시트
            // 갱신"할 때 재현됨). 그래서 파일 쓰기까지만 동기로 하고, 실제 재임포트는
            // 지금 콜백이 완전히 끝난 다음 프레임으로 미룹니다.
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(LocKeyOutputPath);
                AssetDatabase.ImportAsset(LanguageOutputPath);
                AssetDatabase.Refresh();

                Debug.Log($"[LocalizationGenerator] 생성됨: ELocKey({keyNames.Count}개), ELanguage({languageColumns.Count}개)");
            };
        }

        private static SerializedProperty FindTableProperty(SerializedObject so)
        {
            SerializedProperty prop = so.FindProperty("_table");
            if (prop != null && prop.isArray)
            {
                return prop;
            }

            prop = so.FindProperty("table");
            if (prop != null && prop.isArray)
            {
                return prop;
            }

            prop = so.FindProperty("Table");
            if (prop != null && prop.isArray)
            {
                return prop;
            }

            return null;
        }

        private static List<string> ExtractKeyNames(SerializedProperty tableProp)
        {
            List<string> list = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < tableProp.arraySize; i++)
            {
                SerializedProperty item = tableProp.GetArrayElementAtIndex(i);
                string keyName = GetString(item, "KeyName");

                if (string.IsNullOrEmpty(keyName))
                {
                    continue;
                }

                // 같은 KeyName이 시트에 두 번 이상 있으면, 나중에 LocalizationManager가
                // 마지막 행의 번역으로 앞의 행을 조용히 덮어씁니다 - 알아채기 어려운
                // 데이터 유실이라 여기서 미리 경고합니다.
                if (!seen.Add(keyName))
                {
                    Debug.LogWarning($"[LocalizationGenerator] KeyName \"{keyName}\"이(가) 시트에 중복으로 있습니다. 마지막 행의 번역만 사용되고 나머지는 무시됩니다.");
                }

                list.Add(keyName);
            }

            return list;
        }

        // 언어 컬럼은 첫 번째 행 하나만 봐도 됩니다 - Data Parsing이 생성하는 Data
        // 클래스는 모든 행이 같은 필드 구조를 공유하기 때문입니다. RowKey(int)는
        // string이 아니라서 자동으로 제외되고, KeyName은 이름으로 명시적으로 제외합니다.
        private static List<string> ExtractLanguageColumns(SerializedProperty firstItem)
        {
            List<string> list = new List<string>();

            SerializedProperty iterator = firstItem.Copy();
            SerializedProperty end = firstItem.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.propertyType == SerializedPropertyType.String && iterator.name != "KeyName")
                {
                    list.Add(iterator.name);
                }
            }

            return list;
        }

        private static string GetString(SerializedProperty root, string name)
        {
            SerializedProperty p = root.FindPropertyRelative(name);
            if (p == null || p.propertyType != SerializedPropertyType.String)
            {
                return null;
            }

            return p.stringValue;
        }

        // 새로 생성할 때 이름을 알파벳순으로 정렬하면, enum은 값을 안 주면 선언 순서대로
        // 0,1,2...가 매겨지기 때문에 기존 멤버의 정수 값이 조용히 바뀌어버립니다 (저장된
        // 언어 선택, Inspector에 지정해둔 ELocKey 등이 다른 항목을 가리키게 됨). 그래서
        // 기존 멤버는 시트에서 지워졌더라도 순서/값을 그대로 보존하고, 새로 추가된
        // 이름만 맨 뒤에 붙입니다. (Sound의 ESoundGenerator와 동일한 정책입니다.)
        private static List<string> BuildOrderedNames(List<string> sheetNames, string[] existingNames)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < existingNames.Length; i++)
            {
                string name = existingNames[i];
                if (name == "None" || !seen.Add(name))
                {
                    continue;
                }

                result.Add(name);
            }

            for (int i = 0; i < sheetNames.Count; i++)
            {
                string raw = sheetNames[i];
                string safe = MakeEnumName(raw);

                if (string.IsNullOrEmpty(safe))
                {
                    continue;
                }

                if (safe == "None")
                {
                    // "None"은 항상 0번 값으로 미리 예약되어 있어서(비어있는/미설정
                    // 상태를 나타냄), 시트에 "None"이라는 이름의 KeyName이나 언어
                    // 컬럼이 있으면 이 예약된 값과 조용히 합쳐져 버립니다.
                    Debug.LogWarning($"[LocalizationGenerator] \"{raw}\"은(는) 예약된 이름 \"None\"과 겹쳐서 별도 멤버로 생성되지 않고 기존 None(0)과 합쳐집니다. 시트의 이름을 바꿔주세요.");
                    continue;
                }

                if (safe != raw)
                {
                    // LocalizationManager는 런타임에 테이블에 적힌 원본 이름 문자열로
                    // Enum.TryParse하기 때문에, 여기서 이름이 바뀌면(공백/하이픈 등) 그
                    // 항목은 절대 조회되지 않습니다. 조용히 넘어가지 않고 바로 알려줍니다.
                    Debug.LogError($"[LocalizationGenerator] \"{raw}\"은(는) 유효한 식별자가 아니라서 enum 멤버는 \"{safe}\"로 생성되지만, 런타임에는 원본 이름으로 조회하므로 이 항목은 인식되지 않습니다. 시트의 KeyName/언어 컬럼명을 영문/숫자/밑줄로만 바꿔주세요.");
                }

                if (seen.Add(safe))
                {
                    result.Add(safe);
                }
            }

            return result;
        }

        private static string BuildCode(string enumName, List<string> names)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("// 자동 생성됨. 직접 편집하지 마세요.");
            sb.AppendLine();
            sb.AppendLine("namespace GameFramework.Localization");
            sb.AppendLine("{");
            sb.AppendLine("    public enum " + enumName);
            sb.AppendLine("    {");
            sb.AppendLine("        None = 0,");

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];

                if (name == "None")
                {
                    continue;
                }

                sb.Append("        ");
                sb.Append(name);
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

        private static string MakeEnumName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string s = raw.Trim();
            s = s.Replace(" ", "_");
            s = s.Replace("-", "_");

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                bool ok =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    (c == '_');

                if (ok)
                {
                    sb.Append(c);
                }
            }

            string result = sb.ToString();
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            char first = result[0];
            if (first >= '0' && first <= '9')
            {
                result = "_" + result;
            }

            return result;
        }

        private static ScriptableObject FindLocalizationTableSo()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject LocalizationTable");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            }

            return null;
        }
    }
}
