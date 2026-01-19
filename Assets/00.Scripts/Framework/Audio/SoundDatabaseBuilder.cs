#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class SoundDatabaseBuilder
{
    private const string DefaultAudioFolder = "Assets/Audio";
    private const string DefaultAddressablesGroup = "Audio";

    [MenuItem("Framework/Audio/Build Sound Database From Sheet + Folder")]
    public static void Build()
    {
        SoundDatabaseSO database = FindSingleAsset<SoundDatabaseSO>();
        if (database == null)
        {
            Debug.LogError("SoundDatabaseSO not found. Create one: Create > Framework > Audio > SoundDatabaseSO");
            return;
        }

        ScriptableObject soundTable = FindSoundTableSo();
        if (soundTable == null)
        {
            Debug.LogError("Sound table SO not found. Make sure GoogleSheet parsing created a Sound table SO (e.g., SoundTable or Sound).");
            return;
        }

        List<Row> rows = ExtractRows(soundTable);
        if (rows.Count == 0)
        {
            Debug.LogError("No rows extracted from Sound table. Need fields: FileName, Channel, DefaultVolume, MaxConcurrent, Loop.");
            return;
        }

        Dictionary<string, string> clipPathByName = ScanAudioFolder(DefaultAudioFolder);
        List<SoundDatabaseSO.Entry> entries = new List<SoundDatabaseSO.Entry>();

        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];

            ESound id;
            bool parsed = EnumTryParse(r.fileName, out id);
            if (parsed == false)
            {
                Debug.LogWarning("[SoundDatabaseBuilder] FileName is not a valid ESound enum name: " + r.fileName);
                continue;
            }

            SoundDatabaseSO.Entry e = new SoundDatabaseSO.Entry();
            e.id = id;
            e.channel = r.channel;
            e.fileName = r.fileName;
            e.defaultVolume = r.defaultVolume;
            e.maxConcurrent = r.maxConcurrent;
            e.loop = r.loop;

            entries.Add(e);

            string clipAssetPath = null;
            if (clipPathByName.ContainsKey(e.fileName))
            {
                clipAssetPath = clipPathByName[e.fileName];
            }

            if (string.IsNullOrEmpty(clipAssetPath))
            {
                Debug.LogWarning("[SoundDatabaseBuilder] Clip not found in folder. FileName: " + e.fileName);
                continue;
            }

            EnsureAddressable(clipAssetPath, DefaultAddressablesGroup, e.fileName);
        }

        database.SetEntries(entries);
        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SoundDatabaseBuilder] Build completed. Entries: " + entries.Count);
    }

    private static Dictionary<string, string> ScanAudioFolder(string folder)
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

            string name = clip.name;
            if (map.ContainsKey(name) == false)
            {
                map.Add(name, path);
            }
        }

        return map;
    }

    private static void EnsureAddressable(string assetPath, string groupName, string address)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found. Create Addressables settings first.");
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
        else
        {
            if (entry.parentGroup != group)
            {
                settings.MoveEntry(entry, group);
            }
        }

        if (entry.address != address)
        {
            entry.address = address;
            EditorUtility.SetDirty(settings);
        }
    }

    private static bool EnumTryParse(string raw, out ESound id)
    {
        id = ESound.None;

        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        try
        {
            id = (ESound)Enum.Parse(typeof(ESound), raw);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private struct Row
    {
        public string fileName;
        public EAudioChannel channel;
        public float defaultVolume;
        public int maxConcurrent;
        public bool loop;
    }

    private static List<Row> ExtractRows(ScriptableObject soundTable)
    {
        List<Row> rows = new List<Row>();

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
            return rows;
        }

        for (int i = 0; i < tableProp.arraySize; i++)
        {
            SerializedProperty item = tableProp.GetArrayElementAtIndex(i);

            string fileName = GetString(item, "FileName");
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = GetString(item, "fileName");
            }

            string channelRaw = GetString(item, "Channel");
            if (string.IsNullOrEmpty(channelRaw))
            {
                channelRaw = GetString(item, "channel");
            }

            float defaultVolume = GetFloat(item, "DefaultVolume", 1f);
            int maxConcurrent = GetInt(item, "MaxConcurrent", 3);
            bool loop = GetBool(item, "Loop", false);

            if (string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            Row r = new Row();
            r.fileName = fileName;
            r.channel = ParseChannel(channelRaw);
            r.defaultVolume = defaultVolume;
            r.maxConcurrent = maxConcurrent;
            r.loop = loop;

            rows.Add(r);
        }

        return rows;
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

    private static float GetFloat(SerializedProperty root, string name, float defaultValue)
    {
        SerializedProperty p = root.FindPropertyRelative(name);
        if (p == null)
        {
            return defaultValue;
        }

        if (p.propertyType == SerializedPropertyType.Float)
        {
            return p.floatValue;
        }

        if (p.propertyType == SerializedPropertyType.Integer)
        {
            return p.intValue;
        }

        return defaultValue;
    }

    private static int GetInt(SerializedProperty root, string name, int defaultValue)
    {
        SerializedProperty p = root.FindPropertyRelative(name);
        if (p == null)
        {
            return defaultValue;
        }

        if (p.propertyType == SerializedPropertyType.Integer)
        {
            return p.intValue;
        }

        return defaultValue;
    }

    private static bool GetBool(SerializedProperty root, string name, bool defaultValue)
    {
        SerializedProperty p = root.FindPropertyRelative(name);
        if (p == null)
        {
            return defaultValue;
        }

        if (p.propertyType == SerializedPropertyType.Boolean)
        {
            return p.boolValue;
        }

        return defaultValue;
    }

    private static EAudioChannel ParseChannel(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return EAudioChannel.SFX;
        }

        if (raw.Equals("BGM", StringComparison.OrdinalIgnoreCase)) return EAudioChannel.BGM;
        if (raw.Equals("SFX", StringComparison.OrdinalIgnoreCase)) return EAudioChannel.SFX;
        if (raw.Equals("UI", StringComparison.OrdinalIgnoreCase)) return EAudioChannel.UI;
        if (raw.Equals("Voice", StringComparison.OrdinalIgnoreCase)) return EAudioChannel.Voice;
        if (raw.Equals("VOICE", StringComparison.OrdinalIgnoreCase)) return EAudioChannel.Voice;

        return EAudioChannel.SFX;
    }

    private static T FindSingleAsset<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        if (guids.Length <= 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
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
#endif
