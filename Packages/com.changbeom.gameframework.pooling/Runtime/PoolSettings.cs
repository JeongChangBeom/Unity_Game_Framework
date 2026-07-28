using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Pooling
{
    [CreateAssetMenu(menuName = "Game Framework/Pooling/Pool Settings", fileName = "PoolSettings")]
    public class PoolSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/PoolSettings";

        public List<Entry> entries = new();

        [Serializable]
        public sealed class Entry
        {
            public GameObject prefab;

            [Min(0)] public int prewarmCount = 10;

            [Tooltip("0이면 무제한")]
            [Min(0)] public int maxCount = 0;

            public bool autoExpand = true;
        }
    }
}
