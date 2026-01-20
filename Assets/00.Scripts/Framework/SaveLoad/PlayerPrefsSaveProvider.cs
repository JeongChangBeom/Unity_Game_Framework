using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerPrefsSaveProvider : ISaveProvider, ISaveBackupProvider
{
    private const string MainPrefix = "Save/";
    private const string BackupPrefix = "SaveBackup/";
    private const string KeyIndexName = "__keys";

    [Serializable]
    private sealed class KeyList
    {
        public List<string> keys = new List<string>();
    }

    public bool HasKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return PlayerPrefs.HasKey(MainPrefix + key);
    }

    public void Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        string full = MainPrefix + key;

        if (PlayerPrefs.HasKey(full))
        {
            PlayerPrefs.DeleteKey(full);
        }

        RemoveKeyFromIndex(key);
    }

    public void Save<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is null or whitespace.", nameof(key));
        }

        string full = MainPrefix + key;

        Type t = typeof(T);

        if (t == typeof(int))
        {
            int v = (int)(object)value;
            PlayerPrefs.SetInt(full, v);
            AddKeyToIndex(key);
            return;
        }

        if (t == typeof(float))
        {
            float v = (float)(object)value;
            PlayerPrefs.SetFloat(full, v);
            AddKeyToIndex(key);
            return;
        }

        if (t == typeof(bool))
        {
            bool v = (bool)(object)value;
            PlayerPrefs.SetInt(full, v ? 1 : 0);
            AddKeyToIndex(key);
            return;
        }

        if (t == typeof(string))
        {
            string v = (string)(object)value;
            if (v == null)
            {
                v = string.Empty;
            }

            PlayerPrefs.SetString(full, v);
            AddKeyToIndex(key);
            return;
        }

        if (value == null)
        {
            PlayerPrefs.DeleteKey(full);
            RemoveKeyFromIndex(key);
            return;
        }

        string json = JsonUtility.ToJson(value);
        PlayerPrefs.SetString(full, json);
        AddKeyToIndex(key);
    }

    public bool TryLoad<T>(string key, out T value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string full = MainPrefix + key;

        if (!PlayerPrefs.HasKey(full))
        {
            return false;
        }

        Type t = typeof(T);

        if (t == typeof(int))
        {
            object boxed = PlayerPrefs.GetInt(full);
            value = (T)boxed;
            return true;
        }

        if (t == typeof(float))
        {
            object boxed = PlayerPrefs.GetFloat(full);
            value = (T)boxed;
            return true;
        }

        if (t == typeof(bool))
        {
            int n = PlayerPrefs.GetInt(full);
            bool b = n != 0;
            object boxed = b;
            value = (T)boxed;
            return true;
        }

        if (t == typeof(string))
        {
            object boxed = PlayerPrefs.GetString(full, string.Empty);
            value = (T)boxed;
            return true;
        }

        string json = PlayerPrefs.GetString(full, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            T obj = JsonUtility.FromJson<T>(json);
            value = obj;
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public void SaveInt(string key, int value)
    {
        Save<int>(key, value);
    }

    public bool TryLoadInt(string key, out int value)
    {
        return TryLoad<int>(key, out value);
    }

    public void SaveString(string key, string value)
    {
        Save<string>(key, value);
    }

    public bool TryLoadString(string key, out string value)
    {
        return TryLoad<string>(key, out value);
    }

    public void Flush()
    {
        PlayerPrefs.Save();
    }

    public bool HasBackup()
    {
        KeyList list = LoadKeyList(BackupPrefix);
        if (list == null)
        {
            return false;
        }

        if (list.keys == null)
        {
            return false;
        }

        return list.keys.Count > 0;
    }

    public bool BackupNow()
    {
        KeyList main = LoadKeyList(MainPrefix);
        if (main == null)
        {
            main = new KeyList();
        }

        if (main.keys == null)
        {
            main.keys = new List<string>();
        }

        SaveKeyList(BackupPrefix, main);

        for (int i = 0; i < main.keys.Count; i++)
        {
            string k = main.keys[i];
            if (string.IsNullOrWhiteSpace(k))
            {
                continue;
            }

            string src = MainPrefix + k;
            string dst = BackupPrefix + k;

            if (!PlayerPrefs.HasKey(src))
            {
                if (PlayerPrefs.HasKey(dst))
                {
                    PlayerPrefs.DeleteKey(dst);
                }

                continue;
            }

            string s = PlayerPrefs.GetString(src, "__NOT_STRING__");
            if (s != "__NOT_STRING__")
            {
                PlayerPrefs.SetString(dst, s);
                continue;
            }

            int iv = PlayerPrefs.GetInt(src, int.MinValue);
            if (iv != int.MinValue)
            {
                PlayerPrefs.SetInt(dst, iv);
                continue;
            }

            float fv = PlayerPrefs.GetFloat(src, float.NaN);
            if (!float.IsNaN(fv))
            {
                PlayerPrefs.SetFloat(dst, fv);
                continue;
            }
        }

        PlayerPrefs.Save();
        return true;
    }

    public bool RestoreFromBackup()
    {
        KeyList backup = LoadKeyList(BackupPrefix);
        if (backup == null)
        {
            return false;
        }

        if (backup.keys == null)
        {
            return false;
        }

        KeyList main = LoadKeyList(MainPrefix);
        if (main == null)
        {
            main = new KeyList();
        }

        if (main.keys == null)
        {
            main.keys = new List<string>();
        }

        for (int i = 0; i < main.keys.Count; i++)
        {
            string k = main.keys[i];
            if (string.IsNullOrWhiteSpace(k))
            {
                continue;
            }

            string dst = MainPrefix + k;
            if (PlayerPrefs.HasKey(dst))
            {
                PlayerPrefs.DeleteKey(dst);
            }
        }

        main.keys.Clear();

        for (int i = 0; i < backup.keys.Count; i++)
        {
            string k = backup.keys[i];
            if (string.IsNullOrWhiteSpace(k))
            {
                continue;
            }

            string src = BackupPrefix + k;
            string dst = MainPrefix + k;

            if (!PlayerPrefs.HasKey(src))
            {
                continue;
            }

            string s = PlayerPrefs.GetString(src, "__NOT_STRING__");
            if (s != "__NOT_STRING__")
            {
                PlayerPrefs.SetString(dst, s);
                main.keys.Add(k);
                continue;
            }

            int iv = PlayerPrefs.GetInt(src, int.MinValue);
            if (iv != int.MinValue)
            {
                PlayerPrefs.SetInt(dst, iv);
                main.keys.Add(k);
                continue;
            }

            float fv = PlayerPrefs.GetFloat(src, float.NaN);
            if (!float.IsNaN(fv))
            {
                PlayerPrefs.SetFloat(dst, fv);
                main.keys.Add(k);
                continue;
            }
        }

        SaveKeyList(MainPrefix, main);

        PlayerPrefs.Save();
        return true;
    }

    private void AddKeyToIndex(string key)
    {
        KeyList list = LoadKeyList(MainPrefix);
        if (list == null)
        {
            list = new KeyList();
        }

        if (list.keys == null)
        {
            list.keys = new List<string>();
        }

        if (!list.keys.Contains(key))
        {
            list.keys.Add(key);
            SaveKeyList(MainPrefix, list);
        }
    }

    private void RemoveKeyFromIndex(string key)
    {
        KeyList list = LoadKeyList(MainPrefix);
        if (list == null)
        {
            return;
        }

        if (list.keys == null)
        {
            return;
        }

        if (list.keys.Contains(key))
        {
            list.keys.Remove(key);
            SaveKeyList(MainPrefix, list);
        }
    }

    private KeyList LoadKeyList(string prefix)
    {
        string full = prefix + KeyIndexName;

        if (!PlayerPrefs.HasKey(full))
        {
            return null;
        }

        string json = PlayerPrefs.GetString(full, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            KeyList list = JsonUtility.FromJson<KeyList>(json);
            return list;
        }
        catch
        {
            return null;
        }
    }

    private void SaveKeyList(string prefix, KeyList list)
    {
        if (list == null)
        {
            list = new KeyList();
        }

        if (list.keys == null)
        {
            list.keys = new List<string>();
        }

        string json = JsonUtility.ToJson(list);
        PlayerPrefs.SetString(prefix + KeyIndexName, json);
    }
}
