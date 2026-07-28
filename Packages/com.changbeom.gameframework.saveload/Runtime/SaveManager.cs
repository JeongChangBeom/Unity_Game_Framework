using System;
using GameFramework.Core;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    public class SaveManager : MonoSingleton<SaveManager>
    {
        [Header("Storage")]
        [SerializeField] private ESaveStorageMode _storageMode = ESaveStorageMode.JsonFile;
        [SerializeField] private string _saveFileName = "save.json";
        [SerializeField] private string _rootKey = "game";
        [SerializeField] private int _currentVersion = 1;

        [Header("Auto Flush")]
        [SerializeField] private bool _autoFlushEnabled = true;
        [SerializeField] private float _autoFlushIntervalSeconds = 5f;

        [Header("Backup")]
        [SerializeField] private bool _backupOnPause = true;
        [SerializeField] private bool _backupOnQuit = true;
        [SerializeField] private bool _autoRestoreOnInit = true;

        private SaveCore _core;
        private float _dirtyElapsed;

        protected override void OnInitialize()
        {
            _core = new SaveCore(CreateProvider(), _rootKey);
            _core.EnsureMeta(_currentVersion);
        }

        private ISaveProvider CreateProvider()
        {
            switch (_storageMode)
            {
                case ESaveStorageMode.PlayerPrefs:
                    return new PlayerPrefsSaveProvider();

                case ESaveStorageMode.Memory:
                    return new MemorySaveProvider();

                case ESaveStorageMode.Es3:
#if USE_ES3
                    return new ES3SaveProvider(_saveFileName);
#else
                    Debug.LogWarning("[SaveManager] Es3 storage mode selected but USE_ES3 is not defined. Falling back to JsonFile. Install Easy Save 3 and add USE_ES3 to Scripting Define Symbols to use it.");
                    return new JsonFileSaveProvider(_saveFileName, _autoRestoreOnInit);
#endif

                default:
                    return new JsonFileSaveProvider(_saveFileName, _autoRestoreOnInit);
            }
        }

        private void Update()
        {
            if (!_autoFlushEnabled || _core.IsDirty == false)
            {
                _dirtyElapsed = 0f;
                return;
            }

            _dirtyElapsed += Time.unscaledDeltaTime;

            if (_dirtyElapsed >= _autoFlushIntervalSeconds)
            {
                Flush();
                _dirtyElapsed = 0f;
            }
        }

        public SaveKey Domain(string domain)
        {
            return new SaveKey(domain);
        }

        public void Save<T>(string key, T value)
        {
            _core.Save(key, value);
        }

        public bool TryLoad<T>(string key, out T value)
        {
            return _core.TryLoad(key, out value);
        }

        public T LoadOrCreate<T>(string key, Func<T> factory, bool saveIfMissing = true)
        {
            return _core.LoadOrCreate(key, factory, saveIfMissing);
        }

        public bool HasKey(string key)
        {
            return _core.HasKey(key);
        }

        public void Delete(string key)
        {
            _core.Delete(key);
        }

        public void Flush()
        {
            _core.Flush();
        }

        public bool HasBackup()
        {
            return _core.HasBackup();
        }

        public void BackupNow()
        {
            _core.BackupNow();
        }

        public bool RestoreFromBackup()
        {
            return _core.RestoreFromBackup();
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause)
            {
                return;
            }

            Flush();

            if (_backupOnPause)
            {
                BackupNow();
            }
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            Flush();

            if (_backupOnQuit)
            {
                BackupNow();
            }
        }
    }
}
