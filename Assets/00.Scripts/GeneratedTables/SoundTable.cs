using System;
using System.Collections.Generic;
using UnityEngine;
using GameFramework.DataParsing;

// 자동 생성됨. 직접 편집하지 마세요.
public class SoundTable : ScriptableObject
{
    [SerializeField] private List<Data> _table = new List<Data>();
    public IReadOnlyList<Data> Table => _table;

    private Dictionary<int, Data> _cache;
    private bool _cacheBuilt;

    [Serializable]
    public class Data
    {
        public int RowKey;
        public string fileName;
        public string channel;
        public float defaultVolume;
        public int maxConcurrent;
        public bool loop;
    }

    public Data Get(int rowKey)
    {
        BuildCacheIfNeeded();

        Data d;
        if (!_cache.TryGetValue(rowKey, out d))
        {
            return null;
        }

        return d;
    }

    private void BuildCacheIfNeeded()
    {
        if (_cacheBuilt && _cache != null)
        {
            return;
        }

        _cache = new Dictionary<int, Data>();

        for (int i = 0; i < _table.Count; i++)
        {
            Data d = _table[i];
            if (d == null)
            {
                continue;
            }

            _cache[d.RowKey] = d;
        }

        _cacheBuilt = true;
    }

    public void ParseFromTsv(string tsv)
    {
        _table.Clear();
        _cacheBuilt = false;
        _cache = null;

        TsvTable table = TsvParser.Parse(tsv);
        if (table == null)
        {
            return;
        }

        if (table.RowCount < 4)
        {
            return;
        }

        HashSet<int> usedRowKeys = new HashSet<int>();

        for (int r = 3; r < table.RowCount; r++)
        {
            string rowKeyText = table.GetCell(r, 0).Trim();
            if (string.IsNullOrEmpty(rowKeyText))
            {
                continue;
            }

            int rowKey;
            if (!int.TryParse(rowKeyText, out rowKey))
            {
                Debug.LogWarning("[Table] rowKey 파싱 실패: row=" + (r + 1) + ", value=" + rowKeyText);
                continue;
            }

            if (!usedRowKeys.Add(rowKey))
            {
                Debug.LogWarning("[Table] 중복 rowKey 스킵: key=" + rowKey + ", row=" + (r + 1));
                continue;
            }

            Data data = new Data();
            data.RowKey = rowKey;
            {
                string raw = table.GetCell(r, 1).Trim();
                data.fileName = raw;
            }
            {
                string raw = table.GetCell(r, 2).Trim();
                data.channel = raw;
            }
            {
                string raw = table.GetCell(r, 3).Trim();
                data.defaultVolume = TableValueParser.ParseFloat(raw, 0f);
            }
            {
                string raw = table.GetCell(r, 4).Trim();
                data.maxConcurrent = TableValueParser.ParseInt(raw, 0);
            }
            {
                string raw = table.GetCell(r, 5).Trim();
                data.loop = TableValueParser.ParseBool(raw, false);
            }

            _table.Add(data);
        }
    }
}
