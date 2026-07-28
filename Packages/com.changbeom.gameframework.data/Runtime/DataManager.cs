using System;
using System.Collections.Generic;
using GameFramework.Core;
using UnityEngine;

namespace GameFramework.Data
{
    public sealed class DataManager : MonoSingleton<DataManager>
    {
        private readonly Dictionary<Type, ScriptableObject> _cache = new Dictionary<Type, ScriptableObject>();

        /// <summary>
        /// Loads (and caches) a generated table by convention: Resources/GeneratedTables/{typeof(T).Name}.
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
                Debug.LogError($"[DataManager] Table asset not found at Resources/GeneratedTables/{typeof(T).Name}.");
                return null;
            }

            _cache[typeof(T)] = loaded;
            return loaded;
        }
    }
}
