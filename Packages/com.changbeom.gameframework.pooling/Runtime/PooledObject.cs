using UnityEngine;

namespace GameFramework.Pooling
{
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        [SerializeField] private GameObject _originPrefab;
        public GameObject OriginPrefab => _originPrefab;

        private PoolManager _owner;
        private IPoolable[] _poolables;

        /// <summary>Spawn/Despawn마다 GetComponentsInChildren로 다시 훑지 않도록, 생성 시점에 한 번만 캐싱한 목록입니다.</summary>
        public IPoolable[] Poolables => _poolables;

        public void Initialize(PoolManager owner, GameObject originPrefab)
        {
            _owner = owner;
            _originPrefab = originPrefab;
            RefreshPoolables();
        }

        /// <summary>IPoolable 캐시를 다시 스캔합니다. 활성 상태로 있는 동안 동적으로 자식이
        /// 추가/제거된 경우를 반영하기 위해, Pool이 Despawn마다 호출합니다.</summary>
        public void RefreshPoolables()
        {
            _poolables = GetComponentsInChildren<IPoolable>(true);
        }

        public void Despawn()
        {
            if (_owner == null)
            {
                Destroy(gameObject);
                return;
            }

            _owner.Despawn(gameObject);
        }

        // Despawn을 거치지 않고 파괴되는 경우(씬 언로드에 딸려 파괴되는 등 - 예를 들어
        // Spawn 시 parent를 씬 오브젝트로 넘긴 경우 그 씬이 언로드되면 같이 파괴됨)를
        // 위한 안전망입니다. 이게 없으면 Pool의 생성 카운트가 영원히 안 줄어들어서,
        // maxCount에 도달한 풀이 실제로는 살아있는 인스턴스가 없는데도 Spawn을 계속
        // 거부하게 됩니다.
        private void OnDestroy()
        {
            if (_owner != null)
            {
                _owner.NotifyInstanceDestroyed(gameObject, _originPrefab);
            }
        }
    }
}
