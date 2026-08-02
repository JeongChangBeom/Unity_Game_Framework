using System;
using GameFramework.Core;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    [BootPriority(-100)]
    public class SaveManager : MonoSingleton<SaveManager>
    {
        private SaveManagerSettings _settings;
        private SaveCore _core;
        private float _dirtyElapsed;

        protected override void OnInitialize()
        {
            _settings = LoadSettings();

            _core = new SaveCore(CreateProvider(_settings), _settings.RootKey);
            _core.EnsureMeta(_settings.CurrentVersion);
        }

        private static SaveManagerSettings LoadSettings()
        {
            SaveManagerSettings settings = Resources.Load<SaveManagerSettings>(SaveManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[SaveManager] Resources/{SaveManagerSettings.ResourcePath}에서 SaveManagerSettings 에셋을 찾지 못했습니다. 기본값을 사용합니다. Assets/Create/Game Framework/Save Load/Save Manager Settings로 에셋을 만드세요.");
            return ScriptableObject.CreateInstance<SaveManagerSettings>();
        }

        private static ISaveProvider CreateProvider(SaveManagerSettings settings)
        {
            switch (settings.StorageMode)
            {
                case ESaveStorageMode.PlayerPrefs:
                    return new PlayerPrefsSaveProvider();

                case ESaveStorageMode.Memory:
                    return new MemorySaveProvider();

                case ESaveStorageMode.Es3:
#if USE_ES3
                    return new ES3SaveProvider(settings.SaveFileName);
#else
                    Debug.LogWarning("[SaveManager] Es3 저장 방식이 선택되었지만 USE_ES3가 정의되어 있지 않습니다. JsonFile로 대체합니다. 사용하려면 Easy Save 3를 설치하고 Scripting Define Symbols에 USE_ES3를 추가하세요.");
                    return new JsonFileSaveProvider(settings.SaveFileName, settings.AutoRestoreOnInit);
#endif

                default:
                    return new JsonFileSaveProvider(settings.SaveFileName, settings.AutoRestoreOnInit);
            }
        }

        private void Update()
        {
            if (!_settings.AutoFlushEnabled || _core.IsDirty == false)
            {
                _dirtyElapsed = 0f;
                return;
            }

            _dirtyElapsed += Time.unscaledDeltaTime;

            if (_dirtyElapsed >= _settings.AutoFlushIntervalSeconds)
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

        /// <summary>실제로 영속 저장소에 쓰기까지 성공했으면 true입니다. false면 다음 Flush(자동 저장 포함)에서 재시도됩니다.</summary>
        public bool Flush()
        {
            return _core.Flush();
        }

        public bool HasBackup()
        {
            return _core.HasBackup();
        }

        public bool BackupNow()
        {
            return _core.BackupNow();
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

            if (_settings.BackupOnPause)
            {
                BackupNow();
            }
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            Flush();

            if (_settings.BackupOnQuit)
            {
                BackupNow();
            }
        }
    }
}
