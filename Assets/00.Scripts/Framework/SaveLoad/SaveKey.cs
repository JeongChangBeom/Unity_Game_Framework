using System;

public readonly struct SaveKey
{
    public readonly string Value;

    public SaveKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SaveKey cannot be null or whitespace.", nameof(value));
        }

        Value = Normalize(value);
    }

    public SaveKey Join(string child)
    {
        if (string.IsNullOrWhiteSpace(child))
        {
            throw new ArgumentException("Child key cannot be null or whitespace.", nameof(child));
        }

        string joined = Value + "/" + child;
        return new SaveKey(joined);
    }

    public override string ToString()
    {
        return Value;
    }

    private static string Normalize(string key)
    {
        string k = key.Trim();
        k = k.Replace("\\", "/");

        while (k.Contains("//"))
        {
            k = k.Replace("//", "/");
        }

        if (k.StartsWith("/"))
        {
            k = k.Substring(1);
        }

        if (k.EndsWith("/"))
        {
            k = k.Substring(0, k.Length - 1);
        }

        return k;
    }
}
