using System;
using UnityEngine;

public sealed class SaveManager : MonoSingleton<SaveManager>
{
    public bool IsInitialized
    {
        get
        {
            if (_core == null)
            {
                return false;
            }

            return _core.IsInitialized;
        }
    }

    private SaveCore _core;
    private ISaveProvider _provider;

    [SerializeField] private int _currentVersion = 1;
    [SerializeField] private string _rootKey = "game";

    [Header("Auto Flush")]
    [SerializeField] private bool _autoFlushEnabled = true;
    [SerializeField] private float _autoFlushIntervalSeconds = 5f;

    [Header("Backup")]
    [SerializeField] private bool _backupOnPause = true;
    [SerializeField] private bool _backupOnQuit = true;

    [Header("Auto Restore")]
    [SerializeField] private bool _autoRestoreOnInit = true;

    protected override void OnInitialize()
    {
        if (_core == null)
        {
            _core = new SaveCore();
        }

        if (_provider == null)
        {
#if USE_ES3
            ES3Settings settings = new ES3Settings();
            settings.path = "save.es3";
            _provider = new ES3SaveProvider(settings);
#else
            _provider = new PlayerPrefsSaveProvider();
#endif
        }

        if (_autoRestoreOnInit)
        {
            TryAutoRestore();
        }

        if (!_core.IsInitialized)
        {
            _core.Initialize(
                _provider,
                _currentVersion,
                new SaveKey(_rootKey),
                _autoFlushEnabled,
                _autoFlushIntervalSeconds
            );
        }
        else
        {
            _core.ConfigureAutoFlush(_autoFlushEnabled, _autoFlushIntervalSeconds);
        }
    }

    private void TryAutoRestore()
    {
        ISaveBackupProvider backupProvider = _provider as ISaveBackupProvider;
        if (backupProvider == null)
        {
            return;
        }

        bool hasBackup = backupProvider.HasBackup();
        if (!hasBackup)
        {
            return;
        }

        int version;
        bool ok = _provider.TryLoadInt(SaveMeta.SaveVersion.Value, out version);

        if (ok)
        {
            return;
        }

        bool restored = backupProvider.RestoreFromBackup();
        if (restored)
        {
            _provider.Flush();
        }
    }

    public void Initialize(ISaveProvider provider, int currentVersion, SaveKey rootKey)
    {
        Initialize(provider, currentVersion, rootKey, _autoFlushEnabled, _autoFlushIntervalSeconds);
    }

    public void Initialize(
        ISaveProvider provider,
        int currentVersion,
        SaveKey rootKey,
        bool autoFlushEnabled,
        float autoFlushIntervalSeconds
    )
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        _provider = provider;

        if (_core == null)
        {
            _core = new SaveCore();
        }

        if (_autoRestoreOnInit)
        {
            TryAutoRestore();
        }

        _core.Initialize(_provider, currentVersion, rootKey, autoFlushEnabled, autoFlushIntervalSeconds);
    }

    public void ConfigureAutoFlush(bool enabled, float intervalSeconds)
    {
        EnsureCore();
        _core.ConfigureAutoFlush(enabled, intervalSeconds);
    }

    public SaveKey Domain(string domain)
    {
        EnsureCore();
        return _core.Domain(domain);
    }

    public bool HasKey(SaveKey key)
    {
        EnsureCore();
        return _core.HasKey(key);
    }

    public void Delete(SaveKey key)
    {
        EnsureCore();
        _core.Delete(key);
    }

    public void Save<T>(SaveKey key, T value)
    {
        EnsureCore();
        _core.Save(key, value);
    }

    public bool TryLoad<T>(SaveKey key, out T value)
    {
        EnsureCore();
        return _core.TryLoad(key, out value);
    }

    public T LoadOrCreate<T>(SaveKey key, Func<T> createDefault, bool saveIfMissing = true)
    {
        EnsureCore();
        return _core.LoadOrCreate(key, createDefault, saveIfMissing);
    }

    public void Flush()
    {
        EnsureCore();
        _core.Flush();
    }

    public bool HasBackup()
    {
        EnsureCore();

        ISaveBackupProvider backupProvider = _provider as ISaveBackupProvider;
        if (backupProvider == null)
        {
            return false;
        }

        return backupProvider.HasBackup();
    }

    public bool BackupNow()
    {
        EnsureCore();

        ISaveBackupProvider backupProvider = _provider as ISaveBackupProvider;
        if (backupProvider == null)
        {
            return false;
        }

        return backupProvider.BackupNow();
    }

    public bool RestoreFromBackup()
    {
        EnsureCore();

        ISaveBackupProvider backupProvider = _provider as ISaveBackupProvider;
        if (backupProvider == null)
        {
            return false;
        }

        bool ok = backupProvider.RestoreFromBackup();
        if (ok)
        {
            _core.Flush();
        }

        return ok;
    }

    private void Update()
    {
        if (_core == null)
        {
            return;
        }

        if (!_core.IsInitialized)
        {
            return;
        }

        _core.Tick(Time.unscaledDeltaTime);
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            return;
        }

        if (_core == null)
        {
            return;
        }

        if (!_core.IsInitialized)
        {
            return;
        }

        _core.Flush();

        if (_backupOnPause)
        {
            BackupNow();
        }
    }

    protected override void OnApplicationQuit()
    {
        if (_core != null && _core.IsInitialized)
        {
            _core.Flush();

            if (_backupOnQuit)
            {
                BackupNow();
            }
        }

        base.OnApplicationQuit();
    }

    private void EnsureCore()
    {
        if (_core == null || !_core.IsInitialized)
        {
            throw new InvalidOperationException("SaveManager is not initialized.");
        }
    }
}
