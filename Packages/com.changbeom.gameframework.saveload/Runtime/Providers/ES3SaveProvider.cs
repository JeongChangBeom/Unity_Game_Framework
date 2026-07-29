#if USE_ES3
using System.IO;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Easy Save 3를 감싸는 provider. ES3 에셋 설치 + Project Settings -> Player ->
    /// Scripting Define Symbols에 USE_ES3 추가가 필요합니다.
    ///
    /// 참고: 이 프로젝트에는 ES3 에셋이 설치되지 않은 상태로 작성되어, 실제 API 형태
    /// (ES3.Save/Load/KeyExists/DeleteKey)는 ES3 공식 문서상의 관례를 기준으로 했습니다.
    /// USE_ES3를 활성화하면 실제로 설치한 ES3 버전과 맞는지 다시 확인하세요.
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
            return ES3.KeyExists(key, _settings);
        }

        public void DeleteKey(string key)
        {
            if (ES3.KeyExists(key, _settings))
            {
                ES3.DeleteKey(key, _settings);
            }
        }

        public void Set<T>(string key, T value)
        {
            ES3.Save(key, value, _settings);
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default;

            if (!ES3.KeyExists(key, _settings))
            {
                return false;
            }

            value = ES3.Load<T>(key, _settings);
            return true;
        }

        public void Flush()
        {
            // ES3는 Save 호출마다 즉시 기록하므로, 별도로 flush할 것이 없습니다.
        }

        public bool HasBackup()
        {
            return File.Exists(_backupPath);
        }

        public void BackupNow()
        {
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

            File.Copy(_backupPath, _filePath, true);
            return true;
        }
    }
}
#endif
