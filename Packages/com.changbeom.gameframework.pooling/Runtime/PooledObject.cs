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
    }
}
