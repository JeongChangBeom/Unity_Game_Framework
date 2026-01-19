using System;
using System.Collections.Generic;

public sealed class SaveCore
{
    public int CurrentVersion { get; private set; }
    public bool IsInitialized { get; private set; }

    private ISaveProvider _provider;
    private SaveKey _root;
    private readonly List<ISaveMigrator> _migrators = new List<ISaveMigrator>();

    public void RegisterMigrator(ISaveMigrator migrator)
    {
        if (migrator == null)
        {
            throw new ArgumentNullException(nameof(migrator));
        }

        _migrators.Add(migrator);
    }

    public void Initialize(ISaveProvider provider, int currentVersion, SaveKey rootKey)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "CurrentVersion must be >= 1.");
        }

        _provider = provider;
        CurrentVersion = currentVersion;
        _root = rootKey;

        EnsureMetaInitialized();
        RunMigrationsIfNeeded();

        IsInitialized = true;
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
        TouchLastSavedUtc();
    }

    public void Save<T>(SaveKey key, T value)
    {
        EnsureReady();
        _provider.Save(key.Value, value);
        TouchLastSavedUtc();
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
            TouchLastSavedUtc();
        }

        return value;
    }

    public void Flush()
    {
        EnsureReady();
        _provider.Flush();
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

        string createdAt;
        bool hasCreatedAt = _provider.TryLoadString(SaveMeta.CreatedAtUtc.Value, out createdAt);
        if (!hasCreatedAt)
        {
            _provider.SaveString(SaveMeta.CreatedAtUtc.Value, DateTime.UtcNow.ToString("O"));
        }

        string lastSaved;
        bool hasLastSaved = _provider.TryLoadString(SaveMeta.LastSavedAtUtc.Value, out lastSaved);
        if (!hasLastSaved)
        {
            _provider.SaveString(SaveMeta.LastSavedAtUtc.Value, DateTime.UtcNow.ToString("O"));
        }

        _provider.Flush();
    }

    private void RunMigrationsIfNeeded()
    {
        int savedVersion;
        bool ok = _provider.TryLoadInt(SaveMeta.SaveVersion.Value, out savedVersion);

        if (!ok)
        {
            return;
        }

        if (savedVersion >= CurrentVersion)
        {
            return;
        }

        int version = savedVersion;

        int guard = 0;
        while (version < CurrentVersion)
        {
            guard++;
            if (guard > 1000)
            {
                throw new InvalidOperationException("Migration loop guard triggered.");
            }

            ISaveMigrator next = FindNextMigrator(version);
            if (next == null)
            {
                throw new InvalidOperationException("No migrator registered for version " + version + ".");
            }

            next.Migrate(_provider);

            version = next.ToVersion;
            _provider.SaveInt(SaveMeta.SaveVersion.Value, version);
            TouchLastSavedUtc();
            _provider.Flush();
        }
    }

    private ISaveMigrator FindNextMigrator(int fromVersion)
    {
        for (int i = 0; i < _migrators.Count; i++)
        {
            ISaveMigrator m = _migrators[i];
            if (m == null)
            {
                continue;
            }

            if (m.FromVersion == fromVersion && m.ToVersion > m.FromVersion)
            {
                return m;
            }
        }

        return null;
    }

    private void TouchLastSavedUtc()
    {
        _provider.SaveString(SaveMeta.LastSavedAtUtc.Value, DateTime.UtcNow.ToString("O"));
    }
}
