using UnityEngine;

public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T _instance;
    private static bool _isQuitting;

    [SerializeField] private bool _dontDestroyOnLoad = true;

    public static T Instance
    {
        get
        {
            if (_isQuitting)
            {
                return null;
            }

            if (_instance != null)
            {
                return _instance;
            }

            T found = Object.FindFirstObjectByType<T>();
            if (found != null)
            {
                _instance = found;
                _instance.EnsureInitialized();
                return _instance;
            }

            GameObject go = new GameObject(typeof(T).Name);
            _instance = go.AddComponent<T>();
            _instance.EnsureInitialized();
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = (T)this;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        OnInitialize();
    }

    protected virtual void OnInitialize() { }

    protected virtual void OnApplicationQuit()
    {
        _isQuitting = true;
    }
}
