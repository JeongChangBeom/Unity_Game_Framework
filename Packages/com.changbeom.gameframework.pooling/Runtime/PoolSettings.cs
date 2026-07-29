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
            [Tooltip("비워두면 이름 기반 조회(Spawn(string key, ...))를 사용할 수 없습니다.")]
            public string key;

            public GameObject prefab;

            [Min(0)] public int prewarmCount = 10;

            [Tooltip("0이면 무제한")]
            [Min(0)] public int maxCount = 0;

            public bool autoExpand = true;
        }
    }
}
