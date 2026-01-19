using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Framework/Pooling/PoolSettings", fileName = "PoolSettings")]
public class PoolSettings : ScriptableObject
{
    public List<Entry> entries = new();

    [Serializable]
    public sealed class Entry
    {
        public GameObject prefab;

        [Min(0)] public int prewarmCount = 10;

        [Tooltip("0이면 무제한")]
        [Min(0)] public int maxCount = 0;

        public bool autoExpand = true;

        [Tooltip("스폰 시 풀 오브젝트를 넣을 기본 부모(비워두면 PoolManager 아래로 정리됨)")]
        public Transform defaultParent;
    }
}
