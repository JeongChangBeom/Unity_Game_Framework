using System;

public sealed class TimeStore
{
    private const string ROOT = "time";
    private SaveManager _save;

    public TimeStore(SaveManager save)
    {
        if (save == null)
        {
            throw new ArgumentNullException(nameof(save));
        }

        _save = save;
    }

    private string MakeKey(string localKey)
    {
        if (string.IsNullOrWhiteSpace(localKey))
        {
            throw new ArgumentException("Key is null or whitespace.", nameof(localKey));
        }

        return $"{ROOT}/{localKey}";
    }

    public bool Has(string localKey)
    {
        return _save.Has(MakeKey(localKey));
    }

    public void Delete(string localKey)
    {
        _save.Delete(MakeKey(localKey));
    }

    public int GetInt(string localKey, int defaultValue)
    {
        return _save.GetOrDefault(MakeKey(localKey), defaultValue);
    }

    public void SetInt(string localKey, int value)
    {
        _save.Set(MakeKey(localKey), value);
    }

    public long GetLong(string localKey, long defaultValue)
    {
        return _save.GetOrDefault(MakeKey(localKey), defaultValue);
    }

    public void SetLong(string localKey, long value)
    {
        _save.Set(MakeKey(localKey), value);
    }

    public string GetString(string localKey, string defaultValue)
    {
        return _save.GetOrDefault(MakeKey(localKey), defaultValue);
    }

    public void SetString(string localKey, string value)
    {
        if (value == null)
        {
            value = string.Empty;
        }

        _save.Set(MakeKey(localKey), value);
    }

    public void Flush()
    {
        _save.Save();
    }
}