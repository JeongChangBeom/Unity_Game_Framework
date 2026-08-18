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
    // ESound.cs는 패키지 자신의 폴더가 아니라 "이 프로젝트"의 Assets 아래에 생성됩니다.
    // git URL/레지스트리로 설치된 패키지는 읽기 전용 캐시(Library/PackageCache)에서
    // 로드되기 때문에, 패키지 자신의 Runtime 폴더에 프로젝트마다 달라지는 값을 쓰는 건
    // 애초에 성립하지 않습니다. 그래서 enum은 프로젝트 쪽에 생성하고, SoundManager는 이
    // enum을 전혀 모르는 채로 FileName(string) 기반 API만 제공하며, 강타입 호출부
    // (sm.PlaySound(ESound.X))는 같이 생성되는 확장 메서드(ESoundExtensions)로
    // 제공합니다. SceneLoading의 ESceneKey/Pooling의 EPoolKey와 동일한 패턴입니다.
    //
    // Data Parsing이 생성한 Sound 테이블(SoundTable)은 프로젝트 쪽에 있어서 이 패키지가
    // 타입으로 직접 참조할 수 없기 때문에, 리플렉션(SerializedProperty)으로 FileName만
    // 읽어옵니다. SoundManager는 런타임에 같은 테이블을 리플렉션으로 다시 읽어 Channel/
    // Volume 등을 자체 캐시로 만듭니다 (SoundManager.cs 참고).
    public static class ESoundGenerator
    {
        private const string OutputPath = "Assets/00.Scripts/GeneratedFramework/ESound.cs";
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

            // RegisterAddressables도 AssetDatabase/AddressableAssetSettings를 건드리므로
            // GenerateESound와 같은 이유로 다음 프레임으로 미룹니다.
            EditorApplication.delayCall += () => RegisterAddressables(rawNames);
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
                    // SoundManager는 런타임에 테이블에 적힌 원본 FileName 문자열을 그대로
                    // 키로 쓰기 때문에, 여기서 이름이 바뀌면(공백/하이픈 등) enum 멤버로
                    // ESound.X를 호출해도 실제로는 다른 문자열(X)을 찾게 되어 그 사운드는
                    // 절대 재생되지 않습니다. 조용히 넘어가지 않고 바로 알려줍니다.
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

            // OnPostprocessAllAssets 콜백(SoundTableSync) 도중에 AssetDatabase.ImportAsset/
            // Refresh를 바로 부르면 지금 진행 중인 임포트 배치와 겹쳐 에디터가 멈출 수
            // 있으므로(Pooling/Localization과 동일한 이유), 콜백이 완전히 끝난 다음
            // 프레임으로 미룹니다.
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.ImportAsset(OutputPath);
                AssetDatabase.Refresh();

                Debug.Log("[ESoundGenerator] 생성됨: " + OutputPath + " (개수: " + ordered.Count + ")");
            };
        }

        // 새로 생성할 때 이름을 알파벳순으로 정렬하면, enum은 값을 안 주면 선언 순서대로
        // 0,1,2...가 매겨지기 때문에 기존 멤버의 정수 값이 조용히 바뀌어버립니다 (Inspector에
        // 저장해둔 값이나 코드에 박아둔 값이 다른 사운드를 가리키게 됨). 그래서 기존 멤버는
        // 시트에서 지워졌더라도 순서/값을 그대로 보존하고, 새로 추가된 이름만 맨 뒤에 붙입니다.
        private static List<string> BuildOrderedNames(List<string> sheetNames)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            string[] currentNames = GetExistingEnumNames();
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

        // ESound는 이제 프로젝트 쪽(Assets)에 생성되어 이 패키지가 컴파일 타임에 직접
        // 참조할 수 없으므로, 이미 컴파일된 프로젝트 어셈블리들에서 TypeCache로 찾습니다
        // (Data Parsing의 TableClassGenerator.TryFindEnumType과 동일한 패턴). 한 번도
        // 생성된 적 없으면(첫 실행) 못 찾는 게 정상이라 빈 배열을 반환합니다.
        private static string[] GetExistingEnumNames()
        {
            TypeCache.TypeCollection candidates = TypeCache.GetTypesDerivedFrom<Enum>();

            for (int i = 0; i < candidates.Count; i++)
            {
                Type t = candidates[i];

                if (t.Namespace == "GameFramework.SoundSystem" && t.Name == "ESound")
                {
                    return Enum.GetNames(t);
                }
            }

            return Array.Empty<string>();
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
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using GameFramework.SoundSystem;");
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
            sb.AppendLine();
            sb.AppendLine("    // SoundManager는 패키지 쪽 코드라 이 프로젝트 전용 enum을 컴파일 타임에");
            sb.AppendLine("    // 알 수 없습니다. 대신 FileName(string) 기반 API를 감싸는 확장 메서드로");
            sb.AppendLine("    // 강타입 호출부(sm.PlaySound(ESound.X))를 그대로 제공합니다.");
            sb.AppendLine("    public static class ESoundExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void PlaySound(this SoundManager manager, ESound id)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (id == ESound.None)");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            manager.PlaySound(id.ToString());");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static void StopSound(this SoundManager manager, ESound id)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (id == ESound.None)");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            manager.StopSound(id.ToString());");
            sb.AppendLine("        }");
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
