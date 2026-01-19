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

    protected override void OnInitialize()
    {
        if (_core == null)
        {
            _core = new SaveCore();
        }

        if (_provider == null)
        {
            _provider = new MemorySaveProvider();
        }

        if (!_core.IsInitialized)
        {
            _core.Initialize(_provider, _currentVersion, new SaveKey(_rootKey));
        }
    }

    public void Initialize(ISaveProvider provider, int currentVersion, SaveKey rootKey)
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

        _core.Initialize(_provider, currentVersion, rootKey);
    }

    public void RegisterMigrator(ISaveMigrator migrator)
    {
        EnsureCore();
        _core.RegisterMigrator(migrator);
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
    }

    protected override void OnApplicationQuit()
    {
        if (_core != null && _core.IsInitialized)
        {
            _core.Flush();
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
