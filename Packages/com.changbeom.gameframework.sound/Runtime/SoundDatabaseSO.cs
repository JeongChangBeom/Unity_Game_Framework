using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Sound
{
    [CreateAssetMenu(menuName = "Game Framework/Sound/Sound Database")]
    public class SoundDatabaseSO : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/SoundDatabaseSO";

        [Serializable]
        public class Entry
        {
            public ESound id;
            public ESoundChannel channel;

            public string fileName;
            public float defaultVolume = 1f;
            public int maxConcurrent = 3;
            public bool loop;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        private Dictionary<ESound, Entry> _cache;
        private bool _cacheBuilt;

        public bool TryGet(ESound id, out Entry entry)
        {
            entry = null;

            if (id == ESound.None)
            {
                return false;
            }

            BuildCacheIfNeeded();

            if (_cache == null)
            {
                return false;
            }

            if (_cache.ContainsKey(id) == false)
            {
                return false;
            }

            entry = _cache[id];
            return entry != null;
        }

        public void SetEntries(List<Entry> newEntries)
        {
            entries = newEntries ?? new List<Entry>();

            _cacheBuilt = false;
            _cache = null;
        }

        private void OnEnable()
        {
            _cacheBuilt = false;
        }

        private void OnValidate()
        {
            _cacheBuilt = false;
            _cache = null;
        }

        private void BuildCacheIfNeeded()
        {
            if (_cacheBuilt)
            {
                return;
            }

            _cacheBuilt = true;

            if (entries == null)
            {
                entries = new List<Entry>();
            }

            _cache = new Dictionary<ESound, Entry>();

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];

                if (e == null || e.id == ESound.None)
                {
                    continue;
                }

                if (_cache.ContainsKey(e.id))
                {
                    continue;
                }

                _cache.Add(e.id, e);
            }
        }
    }
}
