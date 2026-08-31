#if USE_ES3
using System;
using System.IO;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Easy Save 3를 감싸는 provider. ES3 에셋 설치 + Project Settings -> Player ->
    /// Scripting Define Symbols에 USE_ES3 추가가 필요합니다.
    /// </summary>
    public sealed class ES3SaveProvider : ISaveProvider, ISaveBackupProvider
    {
        private readonly string _fileName;
        private readonly string _filePath;
        private readonly string _backupPath;
        private readonly ES3Settings _settings;

        public ES3SaveProvider(string fileName = "save.es3")
        {
            _fileName = fileName;
            _filePath = Path.Combine(Application.persistentDataPath, fileName);
            _backupPath = _filePath + ".bak";
            _settings = new ES3Settings(fileName);
        }

        public bool HasKey(string key)
        {
            try
            {
                return ES3.KeyExists(key, _settings);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ES3SaveProvider] {key} 존재 확인 실패: {e}");
                return false;
            }
        }

        public void DeleteKey(string key)
        {
            try
            {
                if (ES3.KeyExists(key, _settings))
                {
                    ES3.DeleteKey(key, _settings);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ES3SaveProvider] {key} 삭제 실패: {e}");
            }
        }

        public void Set<T>(string key, T value)
        {
            try
            {
                ES3.Save(key, value, _settings);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ES3SaveProvider] {key} 저장 실패: {e}");
            }
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default;

            try
            {
                if (!ES3.KeyExists(key, _settings))
                {
                    return false;
                }

                value = ES3.Load<T>(key, _settings);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ES3SaveProvider] {key} 로드 실패: {e}");
                return false;
            }
        }

        public bool Flush()
        {
            return true;
        }

        public bool HasBackup()
        {
            return File.Exists(_backupPath);
        }

        public bool BackupNow()
        {
            if (!File.Exists(_filePath))
            {
                return false;
            }

            try
            {
                File.Copy(_filePath, _backupPath, true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ES3SaveProvider] 백업 실패: {e}");
                return false;
            }
        }

        public bool RestoreFromBackup()
        {
            if (!File.Exists(_backupPath))
            {
                return false;
            }

            try
            {
                File.Copy(_backupPath, _filePath, true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ES3SaveProvider] 백업 복구 실패: {e}");
                return false;
            }
        }
    }
}
#endif
