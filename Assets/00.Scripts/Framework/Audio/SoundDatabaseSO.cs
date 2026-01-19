using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Framework/Audio/SoundDatabaseSO")]
public class SoundDatabaseSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ESound id;
        public EAudioChannel channel;

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
        BuildCacheIfNeeded();

        if (_cache.ContainsKey(id))
        {
            entry = _cache[id];
            return true;
        }

        entry = null;
        return false;
    }

    public void SetEntries(List<Entry> newEntries)
    {
        entries = newEntries;
        _cacheBuilt = false;
    }

    private void BuildCacheIfNeeded()
    {
        if (_cacheBuilt)
        {
            return;
        }

        if (_cache == null)
        {
            _cache = new Dictionary<ESound, Entry>();
        }
        else
        {
            _cache.Clear();
        }

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if (e == null)
            {
                continue;
            }

            if (_cache.ContainsKey(e.id))
            {
                continue;
            }

            _cache.Add(e.id, e);
        }

        _cacheBuilt = true;
    }
}