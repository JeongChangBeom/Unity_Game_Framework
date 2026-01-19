#if USE_ES3
using System;

public sealed class ES3SaveProvider : ISaveProvider
{
    private readonly ES3Settings _settings;

    public ES3SaveProvider(ES3Settings settings)
    {
        _settings = settings;
    }

    public bool HasKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return ES3.KeyExists(key, _settings);
    }

    public void Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        bool exists = ES3.KeyExists(key, _settings);
        if (!exists)
        {
            return;
        }

        ES3.DeleteKey(key, _settings);
    }

    public void Save<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is null or whitespace.", nameof(key));
        }

        ES3.Save(key, value, _settings);
    }

    public bool TryLoad<T>(string key, out T value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        bool exists = ES3.KeyExists(key, _settings);
        if (!exists)
        {
            return false;
        }

        try
        {
            value = ES3.Load<T>(key, _settings);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public void SaveInt(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is null or whitespace.", nameof(key));
        }

        ES3.Save(key, value, _settings);
    }

    public bool TryLoadInt(string key, out int value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        bool exists = ES3.KeyExists(key, _settings);
        if (!exists)
        {
            return false;
        }

        try
        {
            value = ES3.Load<int>(key, _settings);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public void SaveString(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is null or whitespace.", nameof(key));
        }

        ES3.Save(key, value, _settings);
    }

    public bool TryLoadString(string key, out string value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        bool exists = ES3.KeyExists(key, _settings);
        if (!exists)
        {
            return false;
        }

        try
        {
            value = ES3.Load<string>(key, _settings);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public void Flush()
    {
        try
        {
            ES3.StoreCachedFile(_settings);
        }
        catch
        {
        }
    }
}
#endif
