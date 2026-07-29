using System;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// 내장 provider들이 공용으로 사용하는 JsonUtility 기반 인코딩/디코딩입니다.
    /// UnityEngine.JsonUtility는 순수 primitive/string이나 루트 레벨 값을 직접 직렬화할 수
    /// 없기 때문에, 모든 값을 작은 제네릭 홀더로 감싼 뒤 처리합니다.
    ///
    /// JsonUtility에서 물려받는 제약: T는 [Serializable]이어야 하고(또는 지원되는 내장
    /// 타입), 다형성을 지원하지 않으며, T 자체가 Dictionary 필드를 가질 수 없습니다
    /// (대신 key/value 항목의 List를 사용하세요).
    /// </summary>
    internal static class JsonUtilityCodec
    {
        [Serializable]
        private sealed class Wrapper<T>
        {
            public T Value;
        }

        public static string ToJson<T>(T value)
        {
            Wrapper<T> wrapper = new Wrapper<T> { Value = value };
            return JsonUtility.ToJson(wrapper);
        }

        public static bool TryFromJson<T>(string json, out T value)
        {
            value = default;

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);

                if (wrapper == null)
                {
                    return false;
                }

                value = wrapper.Value;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonUtilityCodec] {typeof(T).Name} 타입의 json 파싱 실패: {e}");
                return false;
            }
        }
    }
}
