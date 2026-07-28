using System;
using System.Globalization;
using UnityEngine;

namespace GameFramework.Data
{
    /// <summary>
    /// Shared cell-value parsing used by generated table classes. Centralizing this here
    /// means a parsing fix applies to every already-generated table without regenerating
    /// them, and keeps the generated code itself short.
    ///
    /// Arrays use a single comma as the element delimiter (e.g. "1,2,3"). An empty string
    /// parses to an empty (not null) array.
    /// </summary>
    public static class TableValueParser
    {
        private static readonly char[] ArrayDelimiter = { ',' };

        public static int ParseInt(string raw, int defaultValue)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            return int.TryParse(raw, out int v) ? v : defaultValue;
        }

        public static long ParseLong(string raw, long defaultValue)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            return long.TryParse(raw, out long v) ? v : defaultValue;
        }

        public static float ParseFloat(string raw, float defaultValue)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : defaultValue;
        }

        public static double ParseDouble(string raw, double defaultValue)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : defaultValue;
        }

        public static bool ParseBool(string raw, bool defaultValue)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            string lower = raw.ToLowerInvariant();

            if (lower == "1" || lower == "true")
            {
                return true;
            }

            if (lower == "0" || lower == "false")
            {
                return false;
            }

            return defaultValue;
        }

        public static int[] ParseIntArray(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return new int[0];
            }

            string[] parts = raw.Split(ArrayDelimiter);
            int[] result = new int[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = ParseInt(parts[i].Trim(), 0);
            }

            return result;
        }

        public static long[] ParseLongArray(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return new long[0];
            }

            string[] parts = raw.Split(ArrayDelimiter);
            long[] result = new long[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = ParseLong(parts[i].Trim(), 0L);
            }

            return result;
        }

        public static float[] ParseFloatArray(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return new float[0];
            }

            string[] parts = raw.Split(ArrayDelimiter);
            float[] result = new float[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = ParseFloat(parts[i].Trim(), 0f);
            }

            return result;
        }

        public static double[] ParseDoubleArray(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return new double[0];
            }

            string[] parts = raw.Split(ArrayDelimiter);
            double[] result = new double[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = ParseDouble(parts[i].Trim(), 0.0);
            }

            return result;
        }

        public static bool[] ParseBoolArray(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return new bool[0];
            }

            string[] parts = raw.Split(ArrayDelimiter);
            bool[] result = new bool[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = ParseBool(parts[i].Trim(), false);
            }

            return result;
        }

        public static string[] ParseStringArray(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return new string[0];
            }

            string[] parts = raw.Split(ArrayDelimiter);

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }

            return parts;
        }

        /// <summary>
        /// Parses a sheet cell into an enum value. Unlike the primitive Parse* methods,
        /// a value that doesn't match any enum member is treated as a sheet-authoring
        /// mistake: it's logged as an ERROR (not a warning) with the exact table/row/value
        /// so it can't be missed, and the field falls back to default(T) rather than
        /// silently looking like valid data.
        /// </summary>
        public static T ParseEnum<T>(string raw, string context) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(raw))
            {
                return default;
            }

            if (Enum.TryParse(raw, true, out T value) && Enum.IsDefined(typeof(T), value))
            {
                return value;
            }

            Debug.LogError($"[Table] enum 파싱 실패 ({context}): value=\"{raw}\", type={typeof(T).Name}. 시트 값의 오타 여부를 확인하세요.");
            return default;
        }

        public static T[] ParseEnumArray<T>(string raw, string context) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(raw))
            {
                return Array.Empty<T>();
            }

            string[] parts = raw.Split(ArrayDelimiter);
            T[] result = new T[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = ParseEnum<T>(parts[i].Trim(), context + "[" + i + "]");
            }

            return result;
        }
    }
}
