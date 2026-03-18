using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public sealed class SaveCore
{
    private readonly ISaveStorage _storage;
    private Dictionary<string, object> _data;

    private bool _isDirty;

    public SaveCore(ISaveStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        Load();
    }

    public void Set<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is null or empty.", nameof(key));
        }

        _data[key] = value;
        _isDirty = true;
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = default;

        if (!_data.TryGetValue(key, out object obj))
        {
            return false;
        }

        try
        {
            if (obj is T casted)
            {
                value = casted;
                return true;
            }

            value = JsonConvert.DeserializeObject<T>(
                JsonConvert.SerializeObject(obj)
            );

            return true;
        }
        catch
        {
            return false;
        }
    }

    public T GetOrDefault<T>(string key, T defaultValue)
    {
        if (TryGet(key, out T value))
        {
            return value;
        }

        Set(key, defaultValue);
        return defaultValue;
    }

    public bool Has(string key)
    {
        return _data.ContainsKey(key);
    }

    public void Delete(string key)
    {
        if (_data.Remove(key))
        {
            _isDirty = true;
        }
    }

    public void Save()
    {
        if (!_isDirty)
        {
            return;
        }

        string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
        _storage.Save(json);

        _isDirty = false;
    }

    private void Load()
    {
        if (!_storage.Exists())
        {
            _data = new Dictionary<string, object>();
            return;
        }

        string json = _storage.Load();

        if (string.IsNullOrEmpty(json))
        {
            _data = new Dictionary<string, object>();
            return;
        }

        try
        {
            _data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            if (_data == null)
            {
                _data = new Dictionary<string, object>();
            }
        }
        catch
        {
            _data = new Dictionary<string, object>();
        }
    }
}