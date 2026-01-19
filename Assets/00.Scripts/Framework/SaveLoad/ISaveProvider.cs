using System;

public interface ISaveProvider
{
    bool HasKey(string key);
    void Delete(string key);

    void Save<T>(string key, T value);
    bool TryLoad<T>(string key, out T value);

    void SaveString(string key, string value);
    bool TryLoadString(string key, out string value);

    void SaveInt(string key, int value);
    bool TryLoadInt(string key, out int value);

    void Flush();
}
