using System;

public sealed class SaveRepository<T> where T : class, new()
{
    private readonly SaveKey _key;

    public SaveRepository(string domain, string key)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain is null or whitespace.", nameof(domain));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is null or whitespace.", nameof(key));
        }

        _key = SaveManager.Instance.Domain(domain).Join(key);
    }

    public T Load()
    {
        return SaveManager.Instance.LoadOrCreate(
            _key,
            createDefault: () => new T(),
            saveIfMissing: true
        );
    }

    public void Save(T data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        SaveManager.Instance.Save(_key, data);
    }

    public void Reset()
    {
        SaveManager.Instance.Save(_key, new T());
    }
}
