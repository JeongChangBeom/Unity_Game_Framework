using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Stores each key as a JsonUtility-encoded string inside PlayerPrefs. Simplest option,
    /// no file I/O of our own, no external JSON library.
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
            PlayerPrefs.SetString(key, JsonUtilityCodec.ToJson(value));
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default;

            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            return JsonUtilityCodec.TryFromJson(PlayerPrefs.GetString(key), out value);
        }

        public void Flush()
        {
            PlayerPrefs.Save();
        }
    }
}
