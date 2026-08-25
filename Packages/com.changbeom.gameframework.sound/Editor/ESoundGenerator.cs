using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace GameFramework.SoundSystem.Editor
{
    // ESound는 더 이상 이 클래스가 생성하지 않습니다. Sound 시트의 FileName 컬럼 타입을
    // "key:ESound"로 선언하면, Data Parsing의 TableClassGenerator.SyncKeyEnums가 "선택 시트
    // 생성/갱신"마다 자동으로 ESound.cs를 만들거나 갱신합니다(SoundTable.cs와 같은 폴더).
    // SoundTable.Get(ESound key)로 그 사운드의 원본 행(Channel/DefaultVolume 등)을 바로
    // 조회할 수 있습니다. 이 클래스는 이제 Addressables 등록만 담당합니다.
    public static class ESoundGenerator
    {
        private const string DefaultSoundFolder = "Assets/03.Sound";
        private const string DefaultAddressablesGroup = "Sound";

        [MenuItem("Game Framework/Sound System/Register Addressables From Sound Table")]
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

            RegisterAddressables(rawNames);
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
