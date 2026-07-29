using System;
using System.Collections.Generic;
using GameFramework.Core;
using UnityEngine;

namespace GameFramework.DataParsing
{
    public sealed class DataManager : MonoSingleton<DataManager>
    {
        private readonly Dictionary<Type, ScriptableObject> _cache = new Dictionary<Type, ScriptableObject>();

        /// <summary>
        /// 관례 경로 Resources/GeneratedTables/{typeof(T).Name}에서 생성된 테이블을 로드하고 캐싱합니다.
        /// </summary>
        public T GetTable<T>() where T : ScriptableObject
        {
            if (_cache.TryGetValue(typeof(T), out ScriptableObject cached))
            {
                return (T)cached;
            }

            T loaded = TableLoader.Load<T>("GeneratedTables/" + typeof(T).Name);

            if (loaded == null)
            {
                Debug.LogError($"[DataManager] Resources/GeneratedTables/{typeof(T).Name}에서 테이블 에셋을 찾지 못했습니다.");
                return null;
            }

            _cache[typeof(T)] = loaded;
            return loaded;
        }
    }
}
