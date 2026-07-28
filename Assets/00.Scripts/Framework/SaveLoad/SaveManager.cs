using GameFramework.Core;

public class SaveManager : MonoSingleton<SaveManager>
{
    private SaveCore _core;
    private bool _initialized;

    protected override void OnInitialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        ISaveStorage storage = new JsonFileStorage();
        _core = new SaveCore(storage);
    }

    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();

        if (_core != null)
        {
            _core.Save();
        }
    }

    public void Set<T>(string key, T value)
    {
        _core.Set(key, value);
    }

    public bool TryGet<T>(string key, out T value)
    {
        return _core.TryGet(key, out value);
    }

    public T GetOrDefault<T>(string key, T defaultValue)
    {
        return _core.GetOrDefault(key, defaultValue);
    }

    public bool Has(string key)
    {
        return _core.Has(key);
    }

    public void Delete(string key)
    {
        _core.Delete(key);
    }

    public void Save()
    {
        _core.Save();
    }
}