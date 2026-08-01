using System;
using System.Globalization;
using UnityEngine;

namespace GameFramework.DataParsing
{
    /// <summary>
    /// 생성된 테이블 클래스들이 공용으로 사용하는 셀 값 파싱입니다. 이렇게 한 곳에 모아두면
    /// 이미 생성된 모든 테이블을 재생성하지 않고도 파싱 수정 사항이 적용되고, 생성되는
    /// 코드 자체도 짧게 유지됩니다.
    ///
    /// 배열은 콤마 하나를 요소 구분자로 사용합니다 (예: "1,2,3"). 빈 문자열은 (null이 아닌)
    /// 빈 배열로 파싱됩니다.
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
                return Array.Empty<int>();
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
                return Array.Empty<long>();
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
                return Array.Empty<float>();
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
                return Array.Empty<double>();
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
                return Array.Empty<bool>();
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
                return Array.Empty<string>();
            }

            string[] parts = raw.Split(ArrayDelimiter);

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }

            return parts;
        }

        /// <summary>
        /// 시트 셀을 enum 값으로 파싱합니다. 기본 Parse* 메서드들과 달리, 어떤 enum
        /// 멤버와도 일치하지 않는 값은 시트 작성 실수로 간주합니다: 놓치지 않도록
        /// (warning이 아니라) ERROR로 정확한 테이블/행/값과 함께 로그를 남기고, 필드는
        /// 조용히 유효한 데이터처럼 보이는 대신 default(T)로 대체됩니다.
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
