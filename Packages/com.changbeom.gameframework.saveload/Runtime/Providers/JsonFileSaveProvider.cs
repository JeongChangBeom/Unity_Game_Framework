using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// 모든 키를 Application.persistentDataPath 아래 JSON 파일 하나에 저장합니다. 쓰기는
    /// 원자적으로(임시 파일 + File.Replace) 처리되고 이전 정상 파일을 ".bak"으로 자동
    /// 보관합니다. 이 자동 롤링 백업(".bak")과 BackupNow로 만드는 수동 체크포인트
    /// (".manual.bak")은 서로 다른 파일입니다.
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
        private readonly string _manualBackupPath;
        private readonly bool _autoRestoreOnInit;

        private Dictionary<string, string> _data;
        private bool _dirty;

        public JsonFileSaveProvider(string fileName = "save.json", bool autoRestoreOnInit = true)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("fileName이 null이거나 비어 있습니다.", nameof(fileName));
            }

            _filePath = Path.Combine(Application.persistentDataPath, fileName);
            _backupPath = _filePath + ".bak";
            _manualBackupPath = _filePath + ".manual.bak";
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

        public bool Flush()
        {
            if (!_dirty)
            {
                return true;
            }

            bool ok = WriteAtomic(Serialize(_data));
            if (ok)
            {
                _dirty = false;
            }

            return ok;
        }

        public bool HasBackup()
        {
            return File.Exists(_manualBackupPath);
        }

        public bool BackupNow()
        {
            if (_dirty && !Flush())
            {
                Debug.LogError("[JsonFileSaveProvider] 백업 실패: 백업 직전 Flush가 실패했습니다.");
                return false;
            }

            if (!File.Exists(_filePath))
            {
                return false;
            }

            try
            {
                CopyFileDurable(_filePath, _manualBackupPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonFileSaveProvider] 백업 실패: {e}");
                return false;
            }
        }

        public bool RestoreFromBackup()
        {
            if (!File.Exists(_manualBackupPath))
            {
                return false;
            }

            Dictionary<string, string> restored = Deserialize(SafeReadAllText(_manualBackupPath));

            if (restored == null)
            {
                return false;
            }

            try
            {
                CopyFileDurable(_manualBackupPath, _filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonFileSaveProvider] 백업 복구 실패: {e}");
                return false;
            }

            _data = restored;
            _dirty = false;
            return true;
        }

        /// <summary>메모리상 데이터와 파일(본파일 + 자동/수동 백업)을 모두 지웁니다.</summary>
        public void DeleteAll()
        {
            _data = new Dictionary<string, string>();
            _dirty = false;

            DeleteFileIfExists(_filePath);
            DeleteFileIfExists(_backupPath);
            DeleteFileIfExists(_manualBackupPath);
        }

        private static void DeleteFileIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonFileSaveProvider] {path} 삭제 실패: {e}");
            }
        }

        private void Load()
        {
            bool fileExists = File.Exists(_filePath);
            Dictionary<string, string> loaded = fileExists ? Deserialize(SafeReadAllText(_filePath)) : null;

            if (loaded != null)
            {
                _data = loaded;
                return;
            }

            if (!fileExists)
            {
                _data = new Dictionary<string, string>();
                return;
            }

            if (!_autoRestoreOnInit)
            {
                Debug.LogError($"[JsonFileSaveProvider] 저장 파일이 손상되었습니다 ({_filePath}). 자동 복구가 꺼져 있어 빈 저장 데이터로 시작합니다.");
                _data = new Dictionary<string, string>();
                return;
            }

            Debug.LogWarning($"[JsonFileSaveProvider] 저장 파일이 손상되었습니다 ({_filePath}). 백업 파일을 시도합니다 ({_backupPath}).");
            Dictionary<string, string> restored = Deserialize(SafeReadAllText(_backupPath));

            if (restored != null)
            {
                Debug.LogWarning("[JsonFileSaveProvider] 백업 파일에서 복구했습니다.");
                _data = restored;
                return;
            }

            Debug.LogError("[JsonFileSaveProvider] 원본과 백업 모두에서 사용 가능한 저장 데이터를 찾지 못했습니다. 빈 저장 데이터로 시작합니다.");
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
                Debug.LogError($"[JsonFileSaveProvider] {path} 읽기 실패: {e}");
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
                Debug.LogError($"[JsonFileSaveProvider] 파싱 실패: {e}");
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

        private bool WriteAtomic(string json)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);

                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tempPath = _filePath + ".tmp";

                using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    fs.Flush(true);
                }

                if (File.Exists(_filePath))
                {
                    try
                    {
                        File.Replace(tempPath, _filePath, _backupPath);
                    }
                    catch (Exception replaceEx)
                    {
                        Debug.LogWarning($"[JsonFileSaveProvider] 이전 백업({_backupPath}) 생성에 실패해 백업 없이 저장을 재시도합니다: {replaceEx.Message}");
                        File.Replace(tempPath, _filePath, null);
                    }
                }
                else
                {
                    File.Move(tempPath, _filePath);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonFileSaveProvider] 저장 실패: {e}");
                return false;
            }
        }

        private static void CopyFileDurable(string sourcePath, string destPath)
        {
            using (FileStream src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                src.CopyTo(dst);
                dst.Flush(true);
            }
        }
    }
}
