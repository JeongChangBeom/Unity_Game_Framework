using System.Collections.Generic;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// 영구 저장하지 않는 provider (Editor/PlayMode 테스트나 짧은 세션용). 프로세스가 종료되면 데이터가 사라집니다.
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

        public bool Flush()
        {
            return true;
        }
    }
}
