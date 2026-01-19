using System.Collections.Generic;
using UnityEngine;

public sealed class PoolManager : MonoSingleton<PoolManager>
{
    [Header("Option")]
    [SerializeField] private PoolSettings _settings;

    private readonly Dictionary<GameObject, Pool> _pools = new Dictionary<GameObject, Pool>();
    private readonly Dictionary<GameObject, GameObject> _instanceToPrefab = new Dictionary<GameObject, GameObject>();

    private Transform _poolRoot;

    protected override void OnInitialize()
    {
        _poolRoot = new GameObject("[PoolRoot]").transform;
        _poolRoot.SetParent(transform, false);

        if (_settings != null)
        {
            BuildFromSettings(_settings);
        }
    }

    public void BuildFromSettings(PoolSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        if (settings.entries == null)
        {
            return;
        }

        for (int i = 0; i < settings.entries.Count; i++)
        {
            PoolSettings.Entry e = settings.entries[i];
            if (e == null)
            {
                continue;
            }

            if (e.prefab == null)
            {
                continue;
            }

            CreatePoolIfNeeded(e.prefab, e.prewarmCount, e.maxCount, e.autoExpand, e.defaultParent);
        }
    }

    public void CreatePoolIfNeeded(GameObject prefab, int prewarmCount, int maxCount, bool autoExpand, Transform defaultParent)
    {
        if (prefab == null)
        {
            return;
        }

        if (_pools.ContainsKey(prefab) == true)
        {
            return;
        }

        Transform root = new GameObject("[Pool] " + prefab.name).transform;
        root.SetParent(_poolRoot, false);

        Pool pool = new Pool(prefab, root, defaultParent, prewarmCount, maxCount, autoExpand, this);
        _pools.Add(prefab, pool);
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
        {
            return null;
        }

        Pool pool;
        if (_pools.TryGetValue(prefab, out pool) == false)
        {
            CreatePoolIfNeeded(prefab, 0, 0, true, null);

            if (_pools.TryGetValue(prefab, out pool) == false)
            {
                return null;
            }
        }

        GameObject instance = pool.Spawn(this, position, rotation, parent);
        if (instance == null)
        {
            return null;
        }

        if (_instanceToPrefab.ContainsKey(instance) == false)
        {
            _instanceToPrefab.Add(instance, prefab);
        }

        return instance;
    }

    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Spawn(prefab.gameObject, position, rotation, parent);
        if (instance == null)
        {
            return null;
        }

        return instance.GetComponent<T>();
    }

    public void Despawn(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        GameObject prefab;
        if (_instanceToPrefab.TryGetValue(instance, out prefab) == false)
        {
            PooledObject pooled = instance.GetComponent<PooledObject>();
            if (pooled != null && pooled.OriginPrefab != null)
            {
                prefab = pooled.OriginPrefab;
            }
        }

        if (prefab == null)
        {
            Destroy(instance);
            return;
        }

        Pool pool;
        if (_pools.TryGetValue(prefab, out pool) == false)
        {
            Destroy(instance);
            return;
        }

        pool.Despawn(instance);
    }
}
