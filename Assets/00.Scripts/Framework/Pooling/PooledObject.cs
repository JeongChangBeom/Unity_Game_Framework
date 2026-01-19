using UnityEngine;

[DisallowMultipleComponent]
public sealed class PooledObject : MonoBehaviour
{
    [SerializeField] private GameObject _originPrefab;
    public GameObject OriginPrefab => _originPrefab;

    private PoolManager _owner;

    public void Initialize(PoolManager owner, GameObject originPrefab)
    {
        _owner = owner;
        _originPrefab = originPrefab;
    }

    public void Despawn()
    {
        if(_owner == null)
        {
            Destroy(gameObject);
            return;
        }

        _owner.Despawn(gameObject);
    }
}
