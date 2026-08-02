using System.Collections.Generic;
using GameFramework.Core;
using UnityEngine;

namespace GameFramework.Pooling
{
    public sealed class PoolManager : MonoSingleton<PoolManager>
    {
        private readonly Dictionary<GameObject, Pool> _pools = new Dictionary<GameObject, Pool>();
        private readonly Dictionary<GameObject, GameObject> _instanceToPrefab = new Dictionary<GameObject, GameObject>();
        private readonly Dictionary<string, GameObject> _keyToPrefab = new Dictionary<string, GameObject>();

        private Transform _poolRoot;

        protected override void OnInitialize()
        {
            _poolRoot = new GameObject("[PoolRoot]").transform;
            _poolRoot.SetParent(transform, false);

            PoolSettings settings = Resources.Load<PoolSettings>(PoolSettings.ResourcePath);

            if (settings != null)
            {
                BuildFromSettings(settings);
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

                CreatePoolIfNeeded(e.prefab, e.prewarmCount, e.maxCount, e.autoExpand);
                RegisterKey(e.key, e.prefab);
            }
        }

        private void RegisterKey(string key, GameObject prefab)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_keyToPrefab.TryGetValue(key, out GameObject existing) && existing != prefab)
            {
                Debug.LogError($"[PoolManager] PoolSettings에 중복된 key가 있습니다: {key}");
                return;
            }

            _keyToPrefab[key] = prefab;
        }

        public bool TryGetPrefab(string key, out GameObject prefab)
        {
            return _keyToPrefab.TryGetValue(key, out prefab);
        }

        public void CreatePoolIfNeeded(GameObject prefab, int prewarmCount, int maxCount, bool autoExpand)
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

            Pool pool = new Pool(prefab, root, prewarmCount, maxCount, autoExpand, this);
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
                CreatePoolIfNeeded(prefab, 0, 0, true);

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

        /// <summary>PoolSettings의 Entry.key로 등록된 프리팹을 바로 스폰합니다. 프리팹 참조를 두 번 연결할 필요가 없습니다.</summary>
        public GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!TryGetPrefab(key, out GameObject prefab))
            {
                Debug.LogError($"[PoolManager] PoolSettings에 등록되지 않은 key입니다: {key}");
                return null;
            }

            return Spawn(prefab, position, rotation, parent);
        }

        /// <summary>PoolSettings의 Entry.key로 등록된 프리팹을 컴포넌트 타입으로 바로 스폰합니다.</summary>
        public T Spawn<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            GameObject instance = Spawn(key, position, rotation, parent);
            if (instance == null)
            {
                return null;
            }

            return instance.GetComponent<T>();
        }

        /// <summary>
        /// Game Framework/Pooling/Generate EPoolKey From Pool Settings로 생성한 EPoolKey를 사용합니다.
        /// 오타가 나면 컴파일 에러가 나므로, 문자열 오버로드보다 이쪽을 우선 사용하세요.
        /// </summary>
        public GameObject Spawn(EPoolKey key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (key == EPoolKey.None)
            {
                Debug.LogError("[PoolManager] EPoolKey.None으로는 Spawn할 수 없습니다.");
                return null;
            }

            return Spawn(key.ToString(), position, rotation, parent);
        }

        /// <summary>위와 동일하되 컴포넌트 타입으로 바로 반환합니다.</summary>
        public T Spawn<T>(EPoolKey key, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (key == EPoolKey.None)
            {
                Debug.LogError("[PoolManager] EPoolKey.None으로는 Spawn할 수 없습니다.");
                return null;
            }

            return Spawn<T>(key.ToString(), position, rotation, parent);
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
}
