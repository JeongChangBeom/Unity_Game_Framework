using System;
using System.Collections.Generic;
using UnityEngine;

public class SoundPlayerPool
{
    private readonly Transform _parent;
    private readonly int _maxCount;

    private readonly List<SoundPlayer> _pool = new List<SoundPlayer>();
    private readonly List<SoundPlayer> _active = new List<SoundPlayer>();

    public int PoolCount => _pool.Count;
    public int ActiveCount => _active.Count;

    public SoundPlayerPool(Transform parent, int initialCount, int maxCount)
    {
        _parent = parent;
        _maxCount = Mathf.Max(1, maxCount);

        int warm = Mathf.Clamp(initialCount, 0, _maxCount);
        for (int i = 0; i < warm; i++)
        {
            SoundPlayer p = CreateNew(i);
            Release(p);
        }
    }

    public SoundPlayer Rent()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            SoundPlayer p = _pool[i];
            if (p == null)
            {
                _pool.RemoveAt(i);
                i--;
                continue;
            }

            _pool.RemoveAt(i);
            Activate(p);
            return p;
        }

        int total = _pool.Count + _active.Count;
        if (total >= _maxCount)
        {
            return null;
        }

        SoundPlayer created = CreateNew(total);
        Activate(created);
        return created;
    }

    public void Release(SoundPlayer p)
    {
        if (p == null)
        {
            return;
        }

        p.Stop();
        p.gameObject.SetActive(false);

        int idx = _active.IndexOf(p);
        if (idx >= 0)
        {
            _active.RemoveAt(idx);
        }

        _pool.Add(p);
    }

    public void StopAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            SoundPlayer p = _active[i];
            if (p == null)
            {
                _active.RemoveAt(i);
                continue;
            }

            Release(p);
        }
    }

    public void Tick(Action<ESound> onReturned)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            SoundPlayer p = _active[i];
            if (p == null)
            {
                _active.RemoveAt(i);
                continue;
            }

            if (p.IsFinished())
            {
                ESound finished = p.CurrentSound;
                Release(p);

                if (finished != ESound.None)
                {
                    onReturned?.Invoke(finished);
                }
            }
        }
    }

    private void Activate(SoundPlayer p)
    {
        if (p == null)
        {
            return;
        }

        p.gameObject.SetActive(true);
        _active.Add(p);
    }

    private SoundPlayer CreateNew(int index)
    {
        GameObject go = new GameObject("SoundPlayer_" + index);
        if (_parent != null)
        {
            go.transform.SetParent(_parent);
        }

        SoundPlayer p = go.AddComponent<SoundPlayer>();

        AudioSource s = p.Source;
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;

        go.SetActive(false);
        return p;
    }
}
