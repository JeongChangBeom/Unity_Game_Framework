using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Localization.Editor
{
    // Localization 테이블의 언어 컬럼들로 ELanguage.cs를 생성하고, ELocKey(Key 컬럼이
    // "key:ELocKey"로 선언되어 있으면 Data Parsing이 이미 생성해둔 타입)로 GetText를
    // 바로 호출할 수 있는 확장 메서드도 함께 생성합니다.
    public static class LocalizationGenerator
    {
        private const string LanguageOutputPath = "Assets/00.Scripts/GeneratedFramework/ELanguage.cs";
        private const string KeyTextExtensionOutputPath = "Assets/00.Scripts/GeneratedTables/LocalizationKeyTextExtensions.cs";

        [MenuItem("Game Framework/Localization/Generate ELanguage From Localization Table")]
        public static void Generate()
        {
            ScriptableObject table = FindLocalizationTableSo();
            if (table == null)
            {
                Debug.LogError("Localization 테이블 SO를 찾지 못했습니다. Data Parsing으로 Localization 시트를 먼저 생성하세요 (예: 탭 이름 Localization -> 클래스 LocalizationTable).");
                return;
            }

            GenerateKeyTextExtension(table);

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

            List<string> languageColumns = ExtractLanguageColumns(tableProp.GetArrayElementAtIndex(0));

            if (languageColumns.Count == 0)
            {
                Debug.LogError("Localization 테이블에서 언어 컬럼을 찾지 못했습니다 (Id/Key를 제외한 string 컬럼이 없습니다).");
                return;
            }

            List<string> orderedLanguages = BuildOrderedNames(languageColumns, GetExistingEnumNames("ELanguage"));

            WriteFile(LanguageOutputPath, BuildLanguageCode(orderedLanguages));

            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(LanguageOutputPath);
                AssetDatabase.Refresh();

                Debug.Log($"[LocalizationGenerator] 생성됨: ELanguage({orderedLanguages.Count}개)");
            };
        }

        /// <summary>Key 컬럼이 "key:EnumName"으로 선언되어 있으면, 그 enum으로
        /// LocalizationManager.GetText를 바로 호출할 수 있는 확장 메서드를 생성합니다.
        /// 아직 아니면 아무것도 만들지 않습니다 - GetText(string)만 쓰면 됩니다.</summary>
        private static void GenerateKeyTextExtension(ScriptableObject table)
        {
            Type tableType = table.GetType();
            Type keyEnumType = null;

            MethodInfo[] methods = tableType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];

                if (m.Name != "Get")
                {
                    continue;
                }

                ParameterInfo[] parameters = m.GetParameters();

                if (parameters.Length != 1 || parameters[0].ParameterType == typeof(int))
                {
                    continue;
                }

                keyEnumType = parameters[0].ParameterType;
                break;
            }

            if (keyEnumType == null)
            {
                return;
            }

            string enumTypeName = keyEnumType.Name;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// 자동 생성됨. 직접 편집하지 마세요.");
            sb.AppendLine();
            sb.AppendLine("using GameFramework.DataParsing;");
            sb.AppendLine("using GameFramework.Localization;");
            sb.AppendLine();
            sb.AppendLine($"public static class {enumTypeName}TextExtensions");
            sb.AppendLine("{");
            sb.AppendLine($"    public static string GetText(this LocalizationManager manager, {enumTypeName} key)");
            sb.AppendLine("    {");
            sb.AppendLine("        LocalizationTable.Data row = DataManager.Instance.GetTable<LocalizationTable>()?.Get(key);");
            sb.AppendLine("        return manager.GetText(row != null ? row.Key : key.ToString());");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            WriteFile(KeyTextExtensionOutputPath, sb.ToString());

            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(KeyTextExtensionOutputPath);
                AssetDatabase.Refresh();

                Debug.Log($"[LocalizationGenerator] 생성됨: {enumTypeName}TextExtensions");
            };
        }

        private static string[] GetExistingEnumNames(string enumTypeName)
        {
            TypeCache.TypeCollection candidates = TypeCache.GetTypesDerivedFrom<Enum>();

            for (int i = 0; i < candidates.Count; i++)
            {
                Type t = candidates[i];

                if (t.Namespace == "GameFramework.Localization" && t.Name == enumTypeName)
                {
                    return Enum.GetNames(t);
                }
            }

            return Array.Empty<string>();
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

        /// <summary>언어 코드처럼 보이는지에 대한 느슨한 판별입니다(대문자 2~3자, 예: KO/EN/ZH).
        /// 안 맞아도 컬럼을 빼지는 않고 경고만 남깁니다.</summary>
        private static readonly System.Text.RegularExpressions.Regex LanguageCodePattern =
            new System.Text.RegularExpressions.Regex("^[A-Za-z]{2,3}$");

        /// <summary>Id/Key를 제외한 string 타입 컬럼을 전부 "언어 컬럼"으로 간주합니다.</summary>
        private static List<string> ExtractLanguageColumns(SerializedProperty firstItem)
        {
            List<string> list = new List<string>();

            SerializedProperty iterator = firstItem.Copy();
            SerializedProperty end = firstItem.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.propertyType == SerializedPropertyType.String && iterator.name != "Key")
                {
                    if (!LanguageCodePattern.IsMatch(iterator.name))
                    {
                        Debug.LogWarning($"[LocalizationGenerator] 컬럼 \"{iterator.name}\"이(가) 언어 코드처럼 보이지 않습니다(대문자 2~3자 코드 권장, 예: KO/EN/JP). 실제 언어 컬럼이 맞는지, 혹시 메모용 string 컬럼을 Localization 시트에 잘못 추가한 건 아닌지 확인하세요. 언어 컬럼이 맞다면 이 경고는 무시해도 됩니다.");
                    }

                    list.Add(iterator.name);
                }
            }

            return list;
        }

        // 기존 멤버는 시트에서 지워졌더라도 순서/값을 그대로 보존하고, 새로 추가된
        // 이름만 맨 뒤에 붙입니다(기존 ELanguage 값이 다른 언어를 가리키지 않도록).
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
                    Debug.LogWarning($"[LocalizationGenerator] \"{raw}\"은(는) 예약된 이름 \"None\"과 겹쳐서 별도 멤버로 생성되지 않고 기존 None(0)과 합쳐집니다. 시트의 이름을 바꿔주세요.");
                    continue;
                }

                if (safe != raw)
                {
                    Debug.LogError($"[LocalizationGenerator] \"{raw}\"은(는) 유효한 식별자가 아니라서 enum 멤버는 \"{safe}\"로 생성되지만, 런타임에는 원본 이름으로 조회하므로 이 항목은 인식되지 않습니다. 시트의 언어 컬럼명을 영문/숫자/밑줄로만 바꿔주세요.");
                }

                if (seen.Add(safe))
                {
                    result.Add(safe);
                }
            }

            return result;
        }

        private static string BuildLanguageCode(List<string> names)
        {
            StringBuilder sb = new StringBuilder();
            AppendEnumHeader(sb, "ELanguage", names);

            sb.AppendLine();
            sb.AppendLine("    public static class ELanguageExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        public static Awaitable SetLanguageAsync(this LocalizationManager manager, ELanguage language)");
            sb.AppendLine("        {");
            sb.AppendLine("            return manager.SetLanguageAsync(language.ToString());");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void AppendEnumHeader(StringBuilder sb, string enumName, List<string> names)
        {
            sb.AppendLine("// 자동 생성됨. 직접 편집하지 마세요.");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using GameFramework.Localization;");
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
