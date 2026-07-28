using System.Collections.Generic;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Non-persistent provider (Editor/PlayMode tests, or short-lived sessions). Data is lost on process exit.
    /// </summary>
    public sealed class MemorySaveProvider : ISaveProvider
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public bool HasKey(string key)
        {
            return _data.ContainsKey(key);
        }

        public void DeleteKey(string key)
        {
            _data.Remove(key);
        }

        public void Set<T>(string key, T value)
        {
            _data[key] = value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default;

            if (!_data.TryGetValue(key, out object obj))
            {
                return false;
            }

            if (obj is T casted)
            {
                value = casted;
                return true;
            }

            return false;
        }

        public void Flush()
        {
        }
    }
}
