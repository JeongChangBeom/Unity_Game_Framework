using Newtonsoft.Json;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Stores each key as a JSON string inside PlayerPrefs. Simplest option, no file I/O of our own.
    /// </summary>
    public sealed class PlayerPrefsSaveProvider : ISaveProvider
    {
        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        public void Set<T>(string key, T value)
        {
            string json = JsonConvert.SerializeObject(value);
            PlayerPrefs.SetString(key, json);
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default;

            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            try
            {
                value = JsonConvert.DeserializeObject<T>(PlayerPrefs.GetString(key));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Flush()
        {
            PlayerPrefs.Save();
        }
    }
}
