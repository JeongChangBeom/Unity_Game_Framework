using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace GameFramework.SoundSystem.Editor
{
    // ESound.cs는 (Data Parsing이 생성하는 프로젝트 쪽 폴더가 아니라) 이 패키지의 Runtime
    // 폴더 안에서 직접 재생성됩니다. SoundManager가 ESound를 구체 타입으로 참조하기
    // 때문에 같은 어셈블리를 공유해야 하기 때문입니다. 여기서 재생성하면 placeholder가
    // 이 프로젝트의 실제 사운드 id들로 덮어써집니다.
    //
    // Data Parsing이 생성한 Sound 테이블(SoundTable)은 프로젝트 쪽에 있어서 이 패키지가
    // 타입으로 직접 참조할 수 없기 때문에, 리플렉션(SerializedProperty)으로 FileName만
    // 읽어옵니다. SoundManager는 런타임에 같은 테이블을 리플렉션으로 다시 읽어 Channel/
    // Volume 등을 자체 캐시로 만듭니다 (SoundManager.cs 참고).
    public static class ESoundGenerator
    {
        private const string OutputPath = "Packages/com.changbeom.gameframework.sound/Runtime/ESound.cs";
        private const string DefaultSoundFolder = "Assets/03.Sound";
        private const string DefaultAddressablesGroup = "Sound";

        [MenuItem("Game Framework/Sound System/Generate ESound + Register Addressables")]
        public static void Generate()
        {
            ScriptableObject soundTable = FindSoundTableSo();
            if (soundTable == null)
            {
                Debug.LogError("Sound 테이블 SO를 찾지 못했습니다. Data Parsing으로 Sound 시트를 먼저 생성하세요 (예: 탭 이름 Sound -> 클래스 SoundTable).");
                return;
            }

            List<string> rawNames = ExtractFileNames(soundTable);
            if (rawNames.Count == 0)
            {
                Debug.LogError("Sound 테이블에서 FileName 행을 찾지 못했습니다.");
                return;
            }

            GenerateESound(rawNames);
            RegisterAddressables(rawNames);
        }

        private static void GenerateESound(List<string> rawNames)
        {
            HashSet<string> unique = new HashSet<string>();
            List<string> cleaned = new List<string>();

            for (int i = 0; i < rawNames.Count; i++)
            {
                string raw = rawNames[i];
                string safe = MakeEnumName(raw);

                if (string.IsNullOrEmpty(safe))
                {
                    continue;
                }

                if (safe != raw)
                {
                    // SoundManager는 런타임에 테이블에 적힌 원본 FileName 문자열로
                    // Enum.TryParse하기 때문에, 여기서 이름이 바뀌면(공백/하이픈 등) 그
                    // 사운드는 절대 재생되지 않습니다. 조용히 넘어가지 않고 바로 알려줍니다.
                    Debug.LogError($"[ESoundGenerator] FileName \"{raw}\"은(는) 유효한 식별자가 아니라서 enum 멤버는 \"{safe}\"로 생성되지만, 런타임에는 원본 이름으로 조회하므로 이 사운드는 재생되지 않습니다. 시트의 FileName과 오디오 파일명을 영문/숫자/밑줄로만 바꿔주세요.");
                }

                if (!unique.Add(safe))
                {
                    continue;
                }

                cleaned.Add(safe);
            }

            List<string> ordered = BuildOrderedNames(cleaned);

            string code = BuildCode(ordered);
            WriteFile(OutputPath, code);

            AssetDatabase.ImportAsset(OutputPath);
            AssetDatabase.Refresh();

            Debug.Log("[ESoundGenerator] 생성됨: " + OutputPath + " (개수: " + ordered.Count + ")");
        }

        // 새로 생성할 때 이름을 알파벳순으로 정렬하면, enum은 값을 안 주면 선언 순서대로
        // 0,1,2...가 매겨지기 때문에 기존 멤버의 정수 값이 조용히 바뀌어버립니다 (Inspector에
        // 저장해둔 값이나 코드에 박아둔 값이 다른 사운드를 가리키게 됨). 그래서 기존 멤버는
        // 시트에서 지워졌더라도 순서/값을 그대로 보존하고, 새로 추가된 이름만 맨 뒤에 붙입니다.
        private static List<string> BuildOrderedNames(List<string> sheetNames)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            string[] currentNames = Enum.GetNames(typeof(ESound));
            for (int i = 0; i < currentNames.Length; i++)
            {
                string name = currentNames[i];
                if (name == "None" || !seen.Add(name))
                {
                    continue;
                }

                result.Add(name);
            }

            for (int i = 0; i < sheetNames.Count; i++)
            {
                string name = sheetNames[i];
                if (seen.Add(name))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private static void RegisterAddressables(List<string> rawNames)
        {
            Dictionary<string, string> clipPathByName = ScanSoundFolder(DefaultSoundFolder);
            int registered = 0;

            for (int i = 0; i < rawNames.Count; i++)
            {
                string fileName = rawNames[i];

                if (!clipPathByName.TryGetValue(fileName, out string clipAssetPath))
                {
                    Debug.LogWarning("[ESoundGenerator] 폴더에서 클립을 찾지 못했습니다. FileName: " + fileName);
                    continue;
                }

                EnsureAddressable(clipAssetPath, DefaultAddressablesGroup, fileName);
                registered++;
            }

            Debug.Log("[ESoundGenerator] Addressables 등록 완료: " + registered + "개");
        }

        private static Dictionary<string, string> ScanSoundFolder(string folder)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    continue;
                }

                if (map.ContainsKey(clip.name) == false)
                {
                    map.Add(clip.name, path);
                }
            }

            return map;
        }

        private static void EnsureAddressable(string assetPath, string groupName, string address)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[ESoundGenerator] AddressableAssetSettings를 찾지 못했습니다. Addressables 설정을 먼저 생성하세요.");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    true,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);

            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group);
            }
            else if (entry.parentGroup != group)
            {
                settings.MoveEntry(entry, group);
            }

            if (entry.address != address)
            {
                entry.address = address;
                EditorUtility.SetDirty(settings);
            }
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
            sb.AppendLine("namespace GameFramework.SoundSystem");
            sb.AppendLine("{");
            sb.AppendLine("    public enum ESound");
            sb.AppendLine("    {");
            sb.AppendLine("        None = 0,");

            for (int i = 0; i < enumNames.Count; i++)
            {
                string name = enumNames[i];

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

            return null;
        }
    }
}
