using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// 모든 키를 Application.persistentDataPath 아래 JSON 파일 하나에 저장하며,
    /// 외부 JSON 라이브러리 없이 UnityEngine.JsonUtility만 사용합니다. 쓰기는 원자적으로
    /// (임시 파일 + File.Replace) 처리되고, 이전 정상 파일을 ".bak"으로 자동 보관하므로
    /// 쓰는 도중 크래시가 나도 두 사본이 동시에 손상되지 않습니다.
    /// ".bak"(자동 롤링 백업)과 ".manual.bak"(BackupNow로 만드는 수동 체크포인트)은
    /// 서로 다른 파일입니다. 매 Flush마다 갱신되는 ".bak"과 같은 경로를 썼다면,
    /// 이후의 평범한 자동 저장 한 번만으로 사용자가 의도적으로 남긴 체크포인트가
    /// 조용히 덮어써지기 때문에 분리했습니다.
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
                // Flush 실패를 무시하고 그대로 진행하면, 디스크에 있는 오래된 파일을
                // "최신 백업"인 것처럼 복사해버려 실패가 겉으로 드러나지 않습니다.
                Debug.LogError("[JsonFileSaveProvider] 백업 실패: 백업 직전 Flush가 실패했습니다.");
                return false;
            }

            if (!File.Exists(_filePath))
            {
                return false;
            }

            try
            {
                File.Copy(_filePath, _manualBackupPath, true);
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
                File.Copy(_manualBackupPath, _filePath, true);
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
                Debug.LogError($"[JsonFileSaveProvider] 저장 파일이 없거나 손상되었습니다 ({_filePath}). 자동 복구가 꺼져 있어 빈 저장 데이터로 시작합니다.");
                _data = new Dictionary<string, string>();
                return;
            }

            Debug.LogWarning($"[JsonFileSaveProvider] 저장 파일이 없거나 손상되었습니다 ({_filePath}). 백업 파일을 시도합니다 ({_backupPath}).");
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
                File.WriteAllText(tempPath, json);

                if (File.Exists(_filePath))
                {
                    // tempPath를 새 파일로 원자적으로 교체하면서, 동시에 이전 정상 파일을
                    // _backupPath로 옮깁니다 (크래시가 나도 스스로 복구 가능).
                    File.Replace(tempPath, _filePath, _backupPath);
                }
                else
                {
                    File.Move(tempPath, _filePath);
                }

                return true;
            }
            catch (Exception e)
            {
                // 쓰기가 실패하면 dirty 상태를 유지시켜 다음 Flush에서 재시도하도록
                // 호출부(Flush)에 실패를 알립니다. 여기서 삼키면 데이터가 조용히 유실됩니다.
                Debug.LogError($"[JsonFileSaveProvider] 저장 실패: {e}");
                return false;
            }
        }
    }
}
