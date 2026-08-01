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

            if (_inactive.Count > 0)
            {
                go = _inactive.Dequeue();
                _inactiveSet.Remove(go);
            }
            else
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

            // parent가 없으면 -> 씬 루트로 배치합니다. 풀링된(비활성) 인스턴스는 어차피
            // 항상 _root 아래에 있으므로, 이건 활성 인스턴스에만 영향을 줍니다.
            t.SetParent(parent, false);

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

            InvokeOnDespawned(go);

            SetInactive(go);
            _inactive.Enqueue(go);
            _inactiveSet.Add(go);
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

        // GetComponentsInChildren<IPoolable>는 호출마다 새 배열을 할당합니다. 풀링 시스템의
        // 목적 자체가 할당을 줄이는 것이므로, PooledObject.Initialize가 생성 시점에 한 번만
        // 캐싱해둔 배열을 Spawn/Despawn마다 재사용합니다.
        private static IPoolable[] GetPoolables(GameObject go)
        {
            PooledObject pooled = go.GetComponent<PooledObject>();
            return pooled != null ? pooled.Poolables : null;
        }
    }
}
