using System;
using GameFramework.SaveLoad;

namespace GameFramework.TimeSystem
{
    public sealed class TimeStore
    {
        private const string ROOT = "time";
        private readonly SaveManager _save;

        public TimeStore(SaveManager save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            _save = save;
        }

        private string MakeKey(string localKey)
        {
            if (string.IsNullOrWhiteSpace(localKey))
            {
                throw new ArgumentException("Key is null or whitespace.", nameof(localKey));
            }

            return $"{ROOT}/{localKey}";
        }

        public bool Has(string localKey)
        {
            return _save.HasKey(MakeKey(localKey));
        }

        public void Delete(string localKey)
        {
            _save.Delete(MakeKey(localKey));
        }

        public int GetInt(string localKey, int defaultValue)
        {
            return _save.LoadOrCreate(MakeKey(localKey), () => defaultValue, saveIfMissing: true);
        }

        public void SetInt(string localKey, int value)
        {
            _save.Save(MakeKey(localKey), value);
        }

        public long GetLong(string localKey, long defaultValue)
        {
            return _save.LoadOrCreate(MakeKey(localKey), () => defaultValue, saveIfMissing: true);
        }

        public void SetLong(string localKey, long value)
        {
            _save.Save(MakeKey(localKey), value);
        }

        public string GetString(string localKey, string defaultValue)
        {
            return _save.LoadOrCreate(MakeKey(localKey), () => defaultValue, saveIfMissing: true);
        }

        public void SetString(string localKey, string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            _save.Save(MakeKey(localKey), value);
        }

        /// <summary>복잡한(JsonUtility로 직렬화 가능한) 값을 위한 범용 통로입니다. 예: 쿨타임 목록.</summary>
        public T LoadOrCreate<T>(string localKey, Func<T> factory, bool saveIfMissing = true)
        {
            return _save.LoadOrCreate(MakeKey(localKey), factory, saveIfMissing);
        }

        public void Save<T>(string localKey, T value)
        {
            _save.Save(MakeKey(localKey), value);
        }

        public void Flush()
        {
            _save.Flush();
        }
    }
}
