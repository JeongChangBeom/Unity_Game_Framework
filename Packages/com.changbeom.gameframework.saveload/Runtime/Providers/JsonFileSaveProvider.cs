using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Stores every key in a single JSON file under Application.persistentDataPath, using
    /// only UnityEngine.JsonUtility (no external JSON library). Writes are atomic (temp
    /// file + File.Replace) and automatically keep a ".bak" copy of the previous good
    /// file, so a crash mid-write cannot corrupt both copies at once.
    /// </summary>
    public sealed class JsonFileSaveProvider : ISaveProvider, ISaveBackupProvider
    {
        [Serializable]
        private sealed class FileEntry
        {
            public string Key;
            public string Json;
        }

        [Serializable]
        private sealed class FileData
        {
            public List<FileEntry> Entries = new List<FileEntry>();
        }

        private readonly string _filePath;
        private readonly string _backupPath;
        private readonly bool _autoRestoreOnInit;

        private Dictionary<string, string> _data;
        private bool _dirty;

        public JsonFileSaveProvider(string fileName = "save.json", bool autoRestoreOnInit = true)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("fileName is null or empty.", nameof(fileName));
            }

            _filePath = Path.Combine(Application.persistentDataPath, fileName);
            _backupPath = _filePath + ".bak";
            _autoRestoreOnInit = autoRestoreOnInit;

            Load();
        }

        public bool HasKey(string key)
        {
            return _data.ContainsKey(key);
        }

        public void DeleteKey(string key)
        {
            if (_data.Remove(key))
            {
                _dirty = true;
            }
        }

        public void Set<T>(string key, T value)
        {
            _data[key] = JsonUtilityCodec.ToJson(value);
            _dirty = true;
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default;

            if (!_data.TryGetValue(key, out string json))
            {
                return false;
            }

            return JsonUtilityCodec.TryFromJson(json, out value);
        }

        public void Flush()
        {
            if (!_dirty)
            {
                return;
            }

            WriteAtomic(Serialize(_data));
            _dirty = false;
        }

        public bool HasBackup()
        {
            return File.Exists(_backupPath);
        }

        public void BackupNow()
        {
            if (_dirty)
            {
                Flush();
            }

            if (File.Exists(_filePath))
            {
                File.Copy(_filePath, _backupPath, true);
            }
        }

        public bool RestoreFromBackup()
        {
            if (!File.Exists(_backupPath))
            {
                return false;
            }

            Dictionary<string, string> restored = Deserialize(SafeReadAllText(_backupPath));

            if (restored == null)
            {
                return false;
            }

            File.Copy(_backupPath, _filePath, true);
            _data = restored;
            _dirty = false;
            return true;
        }

        private void Load()
        {
            Dictionary<string, string> loaded = Deserialize(SafeReadAllText(_filePath));

            if (loaded != null)
            {
                _data = loaded;
                return;
            }

            if (!_autoRestoreOnInit)
            {
                Debug.LogError($"[JsonFileSaveProvider] Primary save file missing or corrupted ({_filePath}). Auto-restore is disabled, starting with an empty save.");
                _data = new Dictionary<string, string>();
                return;
            }

            Debug.LogWarning($"[JsonFileSaveProvider] Primary save file missing or corrupted ({_filePath}). Trying backup ({_backupPath}).");
            Dictionary<string, string> restored = Deserialize(SafeReadAllText(_backupPath));

            if (restored != null)
            {
                Debug.LogWarning("[JsonFileSaveProvider] Restored from backup file.");
                _data = restored;
                return;
            }

            Debug.LogError("[JsonFileSaveProvider] No usable save data found in primary or backup. Starting with an empty save.");
            _data = new Dictionary<string, string>();
        }

        private static string SafeReadAllText(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonFileSaveProvider] Read failed for {path}: {e}");
                return null;
            }
        }

        private static Dictionary<string, string> Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                FileData fileData = JsonUtility.FromJson<FileData>(json);

                if (fileData?.Entries == null)
                {
                    return null;
                }

                Dictionary<string, string> data = new Dictionary<string, string>();

                for (int i = 0; i < fileData.Entries.Count; i++)
                {
                    FileEntry entry = fileData.Entries[i];
                    data[entry.Key] = entry.Json;
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonFileSaveProvider] Parse failed: {e}");
                return null;
            }
        }

        private static string Serialize(Dictionary<string, string> data)
        {
            FileData fileData = new FileData();

            foreach (KeyValuePair<string, string> kvp in data)
            {
                fileData.Entries.Add(new FileEntry { Key = kvp.Key, Json = kvp.Value });
            }

            return JsonUtility.ToJson(fileData, true);
        }

        private void WriteAtomic(string json)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);

                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(_filePath))
                {
                    // Atomically swaps tempPath in as the new file while moving the previous
                    // good file to _backupPath in the same operation (self-healing on crash).
                    File.Replace(tempPath, _filePath, _backupPath);
                }
                else
                {
                    File.Move(tempPath, _filePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonFileSaveProvider] Save failed: {e}");
            }
        }
    }
}
