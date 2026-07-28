namespace GameFramework.SaveLoad
{
    public interface ISaveProvider
    {
        bool HasKey(string key);
        void DeleteKey(string key);
        void Set<T>(string key, T value);
        bool TryGet<T>(string key, out T value);
        void Flush();
    }
}
