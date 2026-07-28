namespace GameFramework.Pooling
{
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
