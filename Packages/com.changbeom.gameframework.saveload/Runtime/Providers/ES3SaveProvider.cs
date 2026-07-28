#if USE_ES3
using System.IO;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Wraps Easy Save 3. Requires the ES3 asset installed and USE_ES3 added to
    /// Project Settings -> Player -> Scripting Define Symbols.
    ///
    /// NOTE: written without the ES3 asset installed in this project, so the exact
    /// API surface (ES3.Save/Load/KeyExists/DeleteKey) is based on ES3's documented
    /// conventions. Re-check against your installed ES3 version once USE_ES3 is enabled.
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
            // ES3 writes through on every Save call; nothing to flush explicitly.
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
