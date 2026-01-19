using System;

public sealed class SaveCore
{
    public int CurrentVersion { get; private set; }
    public bool IsInitialized { get; private set; }

    public bool AutoFlushEnabled { get; private set; }
    public float AutoFlushIntervalSeconds { get; private set; }

    private ISaveProvider _provider;
    private SaveKey _root;

    private bool _isDirty;
    private float _dirtyElapsed;

    public void Initialize(ISaveProvider provider, int currentVersion, SaveKey rootKey)
    {
        Initialize(provider, currentVersion, rootKey, autoFlushEnabled: false, autoFlushIntervalSeconds: 5f);
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

        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "CurrentVersion must be >= 1.");
        }

        if (autoFlushIntervalSeconds <= 0f)
        {
            autoFlushIntervalSeconds = 1f;
        }

        _provider = provider;
        CurrentVersion = currentVersion;
        _root = rootKey;

        AutoFlushEnabled = autoFlushEnabled;
        AutoFlushIntervalSeconds = autoFlushIntervalSeconds;

        _isDirty = false;
        _dirtyElapsed = 0f;

        EnsureMetaInitialized();

        IsInitialized = true;
    }

    public void ConfigureAutoFlush(bool enabled, float intervalSeconds)
    {
        EnsureReady();

        AutoFlushEnabled = enabled;

        if (intervalSeconds <= 0f)
        {
            intervalSeconds = 1f;
        }

        AutoFlushIntervalSeconds = intervalSeconds;
    }

    public void Tick(float deltaTime)
    {
        EnsureReady();

        if (!AutoFlushEnabled)
        {
            return;
        }

        if (!_isDirty)
        {
            return;
        }

        if (deltaTime < 0f)
        {
            deltaTime = 0f;
        }

        _dirtyElapsed += deltaTime;

        if (_dirtyElapsed >= AutoFlushIntervalSeconds)
        {
            Flush();
        }
    }

    public SaveKey Domain(string domain)
    {
        EnsureReady();

        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain cannot be null or whitespace.", nameof(domain));
        }

        return _root.Join(domain);
    }

    public bool HasKey(SaveKey key)
    {
        EnsureReady();
        return _provider.HasKey(key.Value);
    }

    public void Delete(SaveKey key)
    {
        EnsureReady();
        _provider.Delete(key.Value);
        MarkDirty();
    }

    public void Save<T>(SaveKey key, T value)
    {
        EnsureReady();
        _provider.Save(key.Value, value);
        MarkDirty();
    }

    public bool TryLoad<T>(SaveKey key, out T value)
    {
        EnsureReady();
        return _provider.TryLoad(key.Value, out value);
    }

    public T LoadOrCreate<T>(SaveKey key, Func<T> createDefault, bool saveIfMissing = true)
    {
        EnsureReady();

        T value;
        bool ok = _provider.TryLoad(key.Value, out value);
        if (ok)
        {
            return value;
        }

        if (createDefault == null)
        {
            throw new ArgumentNullException(nameof(createDefault));
        }

        value = createDefault();

        if (saveIfMissing)
        {
            _provider.Save(key.Value, value);
            MarkDirty();
        }

        return value;
    }

    public void Flush()
    {
        EnsureReady();

        if (_isDirty)
        {
            _provider.SaveString(SaveMeta.LastSavedAtUtc.Value, DateTime.UtcNow.ToString("O"));
        }

        _provider.Flush();

        _isDirty = false;
        _dirtyElapsed = 0f;
    }

    private void EnsureReady()
    {
        if (_provider == null)
        {
            throw new InvalidOperationException("SaveCore is not initialized.");
        }
    }

    private void EnsureMetaInitialized()
    {
        int savedVersion;
        bool hasVersion = _provider.TryLoadInt(SaveMeta.SaveVersion.Value, out savedVersion);

        if (!hasVersion)
        {
            _provider.SaveInt(SaveMeta.SaveVersion.Value, CurrentVersion);
            _provider.SaveString(SaveMeta.CreatedAtUtc.Value, DateTime.UtcNow.ToString("O"));
            _provider.SaveString(SaveMeta.LastSavedAtUtc.Value, DateTime.UtcNow.ToString("O"));
            _provider.Flush();
            return;
        }

        if (savedVersion != CurrentVersion)
        {
            _provider.SaveInt(SaveMeta.SaveVersion.Value, CurrentVersion);
            _provider.SaveString(SaveMeta.LastSavedAtUtc.Value, DateTime.UtcNow.ToString("O"));
            _provider.Flush();
        }

        string createdAt;
        bool hasCreatedAt = _provider.TryLoadString(SaveMeta.CreatedAtUtc.Value, out createdAt);
        if (!hasCreatedAt)
        {
            _provider.SaveString(SaveMeta.CreatedAtUtc.Value, DateTime.UtcNow.ToString("O"));
            _provider.Flush();
        }

        string lastSaved;
        bool hasLastSaved = _provider.TryLoadString(SaveMeta.LastSavedAtUtc.Value, out lastSaved);
        if (!hasLastSaved)
        {
            _provider.SaveString(SaveMeta.LastSavedAtUtc.Value, DateTime.UtcNow.ToString("O"));
            _provider.Flush();
        }
    }

    private void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            _dirtyElapsed = 0f;
        }
    }
}
