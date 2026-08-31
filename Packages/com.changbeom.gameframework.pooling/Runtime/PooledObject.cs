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

        private void OnDestroy()
        {
            if (_owner != null)
            {
                _owner.NotifyInstanceDestroyed(gameObject, _originPrefab);
            }
        }
    }
}
