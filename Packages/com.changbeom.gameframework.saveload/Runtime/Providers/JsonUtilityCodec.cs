using System;
using UnityEngine;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// Shared JsonUtility-based encode/decode used by the built-in providers.
    /// UnityEngine.JsonUtility cannot serialize a bare primitive/string or a root-level
    /// value directly, so every value is wrapped in a small generic holder first.
    ///
    /// Limitations inherited from JsonUtility: T must be [Serializable] (or a supported
    /// built-in type), no polymorphism, and T must not itself contain a Dictionary field
    /// (use a List of key/value entries instead).
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
                Debug.LogError($"[JsonUtilityCodec] Failed to parse json for type {typeof(T).Name}: {e}");
                return false;
            }
        }
    }
}
