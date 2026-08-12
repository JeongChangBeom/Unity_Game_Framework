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

                    // maxCount가 prewarmCount보다 작게 설정되어 있으면 CreateNew가 null을
                    // 반환합니다. 여기서 멈추지 않으면 SetInactive(go)가 바로 NRE를 던져
                    // PoolManager.OnInitialize() 전체가 죽습니다.
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

            // 비활성 대기 중인 인스턴스가 Despawn을 거치지 않고 외부에서 파괴된 경우
            // (드물지만 NotifyInstanceDestroyed가 Queue 자체는 못 건드리므로 발생 가능),
            // Dequeue 결과가 이미 파괴된(Unity의 fake-null) 참조일 수 있습니다. 유효한
            // 인스턴스를 찾을 때까지 건너뜁니다.
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

            // parent가 없으면 -> 씬 루트로 배치합니다. 풀링된(비활성) 인스턴스는 어차피
            // 항상 _root 아래에 있으므로, 이건 활성 인스턴스에만 영향을 줍니다.
            // parent가 이미 파괴된(Unity의 fake-null) Transform 참조라면 SetParent에
            // 그대로 넘겼을 때 MissingReferenceException이 날 수 있으므로, Unity의
            // == 연산자로 파괴 여부까지 확인한 뒤 진짜 null로 정규화해서 넘깁니다.
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

            // IPoolable 목록은 Instantiate 시점에 한 번만 캐싱되는데, 활성 상태로 있는 동안
            // 자기 자신의 OnSpawn 등에서 동적으로 자식을 추가하는 프리팹(예: 절차적으로
            // 이펙트를 붙이는 경우)이 있으면 그 자식의 IPoolable이 영원히 안 불립니다.
            // Despawn될 때마다(=다음 재사용 전에) 다시 스캔해서 최신 상태로 갱신합니다.
            if (_pooledByInstance.TryGetValue(go, out PooledObject refreshTarget))
            {
                refreshTarget.RefreshPoolables();
            }

            InvokeOnDespawned(go);

            SetInactive(go);
            _inactive.Enqueue(go);
            _inactiveSet.Add(go);
        }

        /// <summary>Despawn을 거치지 않고(씬 언로드에 딸려가거나, 직접 Destroy() 호출 등)
        /// 인스턴스가 파괴됐을 때 PooledObject.OnDestroy가 호출합니다. 이걸 안 하면
        /// _totalCreated가 영원히 안 줄어들어서, maxCount에 도달한 풀이 실제로는
        /// 살아있는 인스턴스가 없는데도 영구히 Spawn을 거부하게 됩니다.</summary>
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

        // GetComponentsInChildren<IPoolable>는 호출마다 새 배열을 할당하므로, PooledObject.
        // Initialize가 생성 시점에 한 번만 캐싱해둔 배열을 재사용합니다. PooledObject 자체도
        // CreateNew 시점에 한 번만 조회해 _pooledByInstance에 저장해두고, 여기서는
        // GetComponent를 다시 부르지 않고 그 캐시를 그대로 씁니다.
        private IPoolable[] GetPoolables(GameObject go)
        {
            return _pooledByInstance.TryGetValue(go, out PooledObject pooled) ? pooled.Poolables : null;
        }
    }
}
