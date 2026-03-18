using System;
using System.IO;
using UnityEngine;

public sealed class JsonFileStorage : ISaveStorage
{
    private readonly string _filePath;

    public JsonFileStorage(string fileName = "save.json")
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("fileName is null or empty.", nameof(fileName));
        }

        _filePath = Path.Combine(Application.persistentDataPath, fileName);
    }

    public bool Exists()
    {
        return File.Exists(_filePath);
    }

    public string Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            return File.ReadAllText(_filePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[JsonFileStorage] Load failed: {e}");
            return null;
        }
    }

    public void Save(string json)
    {
        try
        {
            string dir = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_filePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[JsonFileStorage] Save failed: {e}");
        }
    }
}