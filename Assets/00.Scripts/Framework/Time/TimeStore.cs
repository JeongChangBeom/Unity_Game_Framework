using System;
using System.Globalization;

public sealed class TimeStore
{
    private readonly SaveManager _save;
    private readonly SaveKey _root;

    public TimeStore(SaveManager save)
    {
        if (save == null)
        {
            throw new ArgumentNullException(nameof(save));
        }

        _save = save;
        _root = _save.Domain("time");
    }

    public SaveKey K(string localKey)
    {
        if (string.IsNullOrWhiteSpace(localKey))
        {
            throw new ArgumentException("Key is null or whitespace.", nameof(localKey));
        }

        return _root.Join(localKey);
    }

    public bool Has(string localKey)
    {
        return _save.HasKey(K(localKey));
    }

    public void Delete(string localKey)
    {
        _save.Delete(K(localKey));
    }

    public int GetInt(string localKey, int defaultValue)
    {
        int v;
        bool ok = _save.TryLoad(K(localKey), out v);
        if (!ok)
        {
            return defaultValue;
        }

        return v;
    }

    public void SetInt(string localKey, int value)
    {
        _save.Save(K(localKey), value);
    }

    public long GetLong(string localKey, long defaultValue)
    {
        string raw;
        bool ok = _save.TryLoad(K(localKey), out raw);
        if (!ok || string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        long v;
        bool parsed = long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        if (!parsed)
        {
            return defaultValue;
        }

        return v;
    }

    public void SetLong(string localKey, long value)
    {
        string s = value.ToString(CultureInfo.InvariantCulture);
        _save.Save(K(localKey), s);
    }

    public string GetString(string localKey, string defaultValue)
    {
        string v;
        bool ok = _save.TryLoad(K(localKey), out v);
        if (!ok || v == null)
        {
            return defaultValue;
        }

        return v;
    }

    public void SetString(string localKey, string value)
    {
        if (value == null)
        {
            value = string.Empty;
        }

        _save.Save(K(localKey), value);
    }

    public void Flush()
    {
        _save.Flush();
    }
}
