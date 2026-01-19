#if USE_ES3
using System;
using System.IO;
using UnityEngine;

public sealed class ES3SaveProvider : ISaveProvider, ISaveBackupProvider
{
    private readonly ES3Settings _settings;
    private readonly ES3Settings _backupSettings;

    public ES3SaveProvider(ES3Settings settings)
    {
        _settings = settings;

        ES3Settings backup = new ES3Settings(settings);
        backup.path = GetDefaultBackupPath(settings.path);
        _backupSettings = backup;
    }

    public ES3SaveProvider(ES3Settings settings, ES3Settings backupSettings)
    {
        _settings = settings;
        _backupSettings = backupSettings;
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

    public bool HasBackup()
    {
        string backupPath;
        bool ok = TryGetFullFilePath(_backupSettings, out backupPath);
        if (!ok)
        {
            return false;
        }

        return File.Exists(backupPath);
    }

    public bool BackupNow()
    {
        try
        {
            Flush();

            string srcPath;
            bool okSrc = TryGetFullFilePath(_settings, out srcPath);

            string dstPath;
            bool okDst = TryGetFullFilePath(_backupSettings, out dstPath);

            if (!okSrc || !okDst)
            {
                return false;
            }

            if (!File.Exists(srcPath))
            {
                return false;
            }

            string dir = Path.GetDirectoryName(dstPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(srcPath, dstPath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool RestoreFromBackup()
    {
        try
        {
            string srcPath;
            bool okSrc = TryGetFullFilePath(_backupSettings, out srcPath);

            string dstPath;
            bool okDst = TryGetFullFilePath(_settings, out dstPath);

            if (!okSrc || !okDst)
            {
                return false;
            }

            if (!File.Exists(srcPath))
            {
                return false;
            }

            string dir = Path.GetDirectoryName(dstPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(srcPath, dstPath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetFullFilePath(ES3Settings settings, out string fullPath)
    {
        fullPath = null;

        if (settings.location != ES3.Location.File)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.path))
        {
            return false;
        }

        if (Path.IsPathRooted(settings.path))
        {
            fullPath = settings.path;
            return true;
        }

        fullPath = Path.Combine(Application.persistentDataPath, settings.path);
        return true;
    }

    private static string GetDefaultBackupPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "save_backup.es3";
        }

        string ext = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(ext))
        {
            return path + "_backup";
        }

        string nameWithoutExt = Path.GetFileNameWithoutExtension(path);
        string dir = Path.GetDirectoryName(path);

        string backupFile = nameWithoutExt + "_backup" + ext;

        if (string.IsNullOrWhiteSpace(dir))
        {
            return backupFile;
        }

        return Path.Combine(dir, backupFile);
    }
}
#endif
