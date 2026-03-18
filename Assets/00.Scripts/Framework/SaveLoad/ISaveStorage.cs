using System;

public interface ISaveStorage
{
    bool Exists();
    string Load();
    void Save(string json);
}