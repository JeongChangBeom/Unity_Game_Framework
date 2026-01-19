#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ESoundGenerator
{
    private const string OutputPath = "Assets/Scripts/Audio/ESound.cs";

    [MenuItem("Framework/Audio/Generate/ESound From SoundTable")]
    public static void Generate()
    {
        ScriptableObject soundTable = FindSoundTableSo();
        if (soundTable == null)
        {
            Debug.LogError("Sound table SO not found. Make sure GoogleSheet parsing created a Sound table SO (e.g., SoundTable or Sound).");
            return;
        }

        List<string> names = ExtractFileNames(soundTable);
        if (names.Count == 0)
        {
            Debug.LogError("No FileName rows found in Sound table.");
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

        Debug.Log("[ESoundGenerator] Generated: " + OutputPath + " (Count: " + cleaned.Count + ")");
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

        sb.AppendLine("// AUTO-GENERATED. DO NOT EDIT.");
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
#endif
