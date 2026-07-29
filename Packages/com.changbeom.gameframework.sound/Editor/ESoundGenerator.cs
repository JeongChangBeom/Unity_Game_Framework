using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.SoundSystem.Editor
{
    // ESound.cs는 (Assets/GeneratedTables가 아니라) 이 패키지의 Runtime 폴더 안에서 직접
    // 재생성됩니다. SoundDatabaseSO/SoundPlayer/SoundManager가 ESound를 구체 타입으로
    // 참조하기 때문에 같은 어셈블리를 공유해야 하기 때문입니다.
    // 여기서 재생성하면 placeholder가 이 프로젝트의 실제 사운드 id들로 덮어써집니다.
    public static class ESoundGenerator
    {
        private const string OutputPath = "Packages/com.changbeom.gameframework.sound/Runtime/ESound.cs";

        [MenuItem("Game Framework/Sound System/Generate ESound From Sound Table")]
        public static void Generate()
        {
            ScriptableObject soundTable = FindSoundTableSo();
            if (soundTable == null)
            {
                Debug.LogError("Sound 테이블 SO를 찾지 못했습니다. Data Parsing으로 Sound 테이블 SO(예: SoundTable 또는 Sound)를 생성했는지 확인하세요.");
                return;
            }

            List<string> names = ExtractFileNames(soundTable);
            if (names.Count == 0)
            {
                Debug.LogError("Sound 테이블에서 FileName 행을 찾지 못했습니다.");
                return;
            }

            HashSet<string> unique = new HashSet<string>();
            List<string> cleaned = new List<string>();

            for (int i = 0; i < names.Count; i++)
            {
                string safe = MakeEnumName(names[i]);
                if (string.IsNullOrEmpty(safe))
                {
                    continue;
                }

                if (unique.Contains(safe))
                {
                    continue;
                }

                unique.Add(safe);
                cleaned.Add(safe);
            }

            cleaned.Sort(StringComparer.Ordinal);

            string code = BuildCode(cleaned);
            WriteFile(OutputPath, code);

            AssetDatabase.ImportAsset(OutputPath);
            AssetDatabase.Refresh();

            Debug.Log("[ESoundGenerator] 생성됨: " + OutputPath + " (개수: " + cleaned.Count + ")");
        }

        private static List<string> ExtractFileNames(ScriptableObject soundTable)
        {
            List<string> list = new List<string>();

            SerializedObject so = new SerializedObject(soundTable);

            SerializedProperty tableProp = so.FindProperty("_table");
            if (tableProp == null || tableProp.isArray == false)
            {
                tableProp = so.FindProperty("table");
            }

            if (tableProp == null || tableProp.isArray == false)
            {
                tableProp = so.FindProperty("Table");
            }

            if (tableProp == null || tableProp.isArray == false)
            {
                return list;
            }

            for (int i = 0; i < tableProp.arraySize; i++)
            {
                SerializedProperty item = tableProp.GetArrayElementAtIndex(i);

                string fileName = GetString(item, "FileName");
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = GetString(item, "fileName");
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                list.Add(fileName);
            }

            return list;
        }

        private static string BuildCode(List<string> enumNames)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("// 자동 생성됨. 직접 편집하지 마세요.");
            sb.AppendLine();
            sb.AppendLine("public enum ESound");
            sb.AppendLine("{");
            sb.AppendLine("    None = 0,");

            for (int i = 0; i < enumNames.Count; i++)
            {
                string name = enumNames[i];

                if (name == "None")
                {
                    continue;
                }

                sb.Append("    ");
                sb.Append(name);
                sb.AppendLine(",");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void WriteFile(string path, string content)
        {
            string dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
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

        private static string GetString(SerializedProperty root, string name)
        {
            SerializedProperty p = root.FindPropertyRelative(name);
            if (p == null)
            {
                return null;
            }

            if (p.propertyType != SerializedPropertyType.String)
            {
                return null;
            }

            return p.stringValue;
        }

        private static ScriptableObject FindSoundTableSo()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject SoundTable");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            }

            guids = AssetDatabase.FindAssets("t:ScriptableObject Sound");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            }

            return null;
        }
    }
}
