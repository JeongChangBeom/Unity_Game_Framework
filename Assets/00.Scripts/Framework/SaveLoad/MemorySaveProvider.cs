using System;
using System.Collections.Generic;

public sealed class MemorySaveProvider : ISaveProvider
{
    private readonly Dictionary<string, object> _store = new Dictionary<string, object>();

    public bool HasKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return _store.ContainsKey(key);
    }

    public void Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (_store.ContainsKey(key))
        {
            _store.Remove(key);
        }
    }

    public void Save<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is null or whitespace.", nameof(key));
        }

        _store[key] = value;
    }

    public bool TryLoad<T>(string key, out T value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        object obj;
        bool exists = _store.TryGetValue(key, out obj);
        if (!exists)
        {
            return false;
        }

        if (obj is T)
        {
            value = (T)obj;
            return true;
        }

        try
        {
            if (obj == null)
            {
                return false;
            }

            object converted = Convert.ChangeType(obj, typeof(T));
            if (converted is T)
            {
                value = (T)converted;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public void SaveInt(string key, int value)
    {
        Save<int>(key, value);
    }

    public bool TryLoadInt(string key, out int value)
    {
        return TryLoad<int>(key, out value);
    }

    public void SaveString(string key, string value)
    {
        Save<string>(key, value);
    }

    public bool TryLoadString(string key, out string value)
    {
        return TryLoad<string>(key, out value);
    }

    public void Flush()
    {
    }
}
