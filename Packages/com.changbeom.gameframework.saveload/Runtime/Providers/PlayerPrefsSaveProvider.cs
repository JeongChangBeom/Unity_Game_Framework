using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// 각 키를 JsonUtility로 인코딩한 문자열로 PlayerPrefs에 저장합니다. 가장 단순한
    /// 방식으로, 자체 파일 I/O도 외부 JSON 라이브러리도 필요하지 않습니다.
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

        public bool Flush()
        {
            PlayerPrefs.Save();
            return true;
        }
    }
}
