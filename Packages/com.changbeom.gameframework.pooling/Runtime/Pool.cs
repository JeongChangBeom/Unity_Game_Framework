using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Pooling
{
    public sealed class Pool
    {
        private readonly GameObject _prefab;
        private readonly Transform _root;

        private readonly int _maxCount;
        private readonly bool _autoExpand;

        private readonly Queue<GameObject> _inactive = new Queue<GameObject>();
        private readonly HashSet<GameObject> _inactiveSet = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, PooledObject> _pooledByInstance = new Dictionary<GameObject, PooledObject>();
        private int _totalCreated;

        public GameObject Prefab => _prefab;

        public Pool(GameObject prefab, Transform root, int prewarmCount, int maxCount, bool autoExpand, PoolManager owner)
        {
            _prefab = prefab;
            _root = root;
            _maxCount = maxCount;
            _autoExpand = autoExpand;

            if (prewarmCount > 0)
            {
                for (int i = 0; i < prewarmCount; i++)
                {
                    GameObject go = CreateNew(owner);

                    if (go == null)
                    {
                        Debug.LogWarning($"[Pool] {_prefab.name}: prewarmCount({prewarmCount})가 maxCount({maxCount})보다 커서 나머지 프리웜을 건너뜁니다.");
                        break;
                    }

                    SetInactive(go);
                    _inactive.Enqueue(go);
                    _inactiveSet.Add(go);
                }
            }
        }

        public bool CanCreateMore()
        {
            if (_maxCount == 0)
            {
                return true;
            }

            if (_totalCreated < _maxCount)
            {
                return true;
            }

            return false;
        }

        public GameObject Spawn(PoolManager owner, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject go = null;

            while (_inactive.Count > 0 && go == null)
            {
                GameObject candidate = _inactive.Dequeue();
                _inactiveSet.Remove(candidate);

                if (candidate != null)
                {
                    go = candidate;
                }
            }

            if (go == null)
            {
                if (_autoExpand == true && CanCreateMore() == true)
                {
                    go = CreateNew(owner);
                }
            }

            if (go == null)
            {
                return null;
            }

            Transform t = go.transform;

            Transform safeParent = parent != null ? parent : null;
            t.SetParent(safeParent, false);

            t.position = position;
            t.rotation = rotation;

            go.SetActive(true);

            InvokeOnSpawned(go);

            return go;
        }

        public void Despawn(GameObject go)
        {
            if (_inactiveSet.Contains(go))
            {
                Debug.LogWarning($"[Pool] 이미 풀에 반환된 오브젝트를 다시 Despawn 시도했습니다: {go.name}");
                return;
            }

            if (_pooledByInstance.TryGetValue(go, out PooledObject refreshTarget))
            {
                refreshTarget.RefreshPoolables();
            }

            InvokeOnDespawned(go);

            SetInactive(go);
            _inactive.Enqueue(go);
            _inactiveSet.Add(go);
        }

        /// <summary>Despawn을 거치지 않고 인스턴스가 파괴됐을 때(씬 언로드 등) 호출합니다.</summary>
        public void NotifyInstanceDestroyed(GameObject go)
        {
            _pooledByInstance.Remove(go);
            _inactiveSet.Remove(go);

            if (_totalCreated > 0)
            {
                _totalCreated--;
            }
        }

        private GameObject CreateNew(PoolManager owner)
        {
            if (CanCreateMore() == false)
            {
                return null;
            }

            GameObject go = Object.Instantiate(_prefab);
            go.name = _prefab.name;

            PooledObject pooled = go.GetComponent<PooledObject>();
            if (pooled == null)
            {
                pooled = go.AddComponent<PooledObject>();
            }

            pooled.Initialize(owner, _prefab);
            _pooledByInstance[go] = pooled;

            _totalCreated++;
            return go;
        }

        private void SetInactive(GameObject go)
        {
            go.SetActive(false);
            go.transform.SetParent(_root, false);
        }

        private void InvokeOnSpawned(GameObject go)
        {
            IPoolable[] list = GetPoolables(go);
            if (list == null)
            {
                return;
            }

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null)
                {
                    list[i].OnSpawn();
                }
            }
        }

        private void InvokeOnDespawned(GameObject go)
        {
            IPoolable[] list = GetPoolables(go);
            if (list == null)
            {
                return;
            }

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null)
                {
                    list[i].OnDespawn();
                }
            }
        }

        private IPoolable[] GetPoolables(GameObject go)
        {
            return _pooledByInstance.TryGetValue(go, out PooledObject pooled) ? pooled.Poolables : null;
        }
    }
}
