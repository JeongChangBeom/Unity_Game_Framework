using System.Collections.Generic;
using UnityEngine;

public class SoundPlayTester : MonoBehaviour
{
    [SerializeField] private SoundDatabaseSO database;
    [SerializeField] private bool playOnStart;
    [SerializeField] private int startIndex;
    [SerializeField] private float sfxVolumeMul = 1f;
    [SerializeField] private float sfxPitch = 1f;

    private List<SoundDatabaseSO.Entry> _entries;
    private int _index;

    private void Start()
    {
        BuildList();

        if (_entries.Count <= 0)
        {
            Debug.LogError("[SoundPlayTester] No entries. Assign SoundDatabaseSO and run Build Sound Database first.");
            return;
        }

        _index = Mathf.Clamp(startIndex, 0, _entries.Count - 1);

        if (playOnStart)
        {
            PlaySelected();
        }
    }

    private void Update()
    {
        if (_entries == null || _entries.Count <= 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            Prev();
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            Next();
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            PlaySelected();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlaySelected();
        }

        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            StopBgm();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            BuildList();
        }

        for (int i = 0; i < 10; i++)
        {
            KeyCode key = KeyCode.Alpha0 + i;
            if (Input.GetKeyDown(key))
            {
                JumpByNumber(i);
            }
        }
    }

    private void OnGUI()
    {
        if (_entries == null || _entries.Count <= 0)
        {
            GUI.Label(new Rect(10, 10, 800, 25), "SoundPlayTester: No entries. Assign SoundDatabaseSO and build database.");
            return;
        }

        int x = 10;
        int y = 10;

        GUI.Label(new Rect(x, y, 900, 25),
            "↑/↓: Select | Enter/Space: Play | `: Stop BGM | R: Reload DB | 0~9: Jump");

        y += 30;

        SoundDatabaseSO.Entry e = _entries[_index];
        GUI.Label(new Rect(x, y, 900, 25),
            "Selected [" + _index + "/" + (_entries.Count - 1) + "] : " + e.id + " / " + e.fileName + " / " + e.channel);

        y += 30;

        if (GUI.Button(new Rect(x, y, 140, 30), "Play Selected"))
        {
            PlaySelected();
        }

        if (GUI.Button(new Rect(x + 150, y, 140, 30), "Prev"))
        {
            Prev();
        }

        if (GUI.Button(new Rect(x + 300, y, 140, 30), "Next"))
        {
            Next();
        }

        if (GUI.Button(new Rect(x + 450, y, 140, 30), "Stop BGM"))
        {
            StopBgm();
        }

        y += 45;

        GUI.Label(new Rect(x, y, 300, 25), "SFX VolumeMul: " + sfxVolumeMul.ToString("0.00"));
        sfxVolumeMul = GUI.HorizontalSlider(new Rect(x + 120, y + 8, 200, 20), sfxVolumeMul, 0f, 2f);

        y += 25;

        GUI.Label(new Rect(x, y, 300, 25), "SFX Pitch: " + sfxPitch.ToString("0.00"));
        sfxPitch = GUI.HorizontalSlider(new Rect(x + 120, y + 8, 200, 20), sfxPitch, 0.5f, 2f);

        y += 35;

        int listHeight = Mathf.Min(18, _entries.Count) * 20;
        GUI.Box(new Rect(x, y, 620, listHeight + 10), "Entries (top 18)");

        int showCount = Mathf.Min(18, _entries.Count);
        int start = Mathf.Clamp(_index - showCount / 2, 0, Mathf.Max(0, _entries.Count - showCount));

        for (int i = 0; i < showCount; i++)
        {
            int idx = start + i;
            SoundDatabaseSO.Entry it = _entries[idx];

            Rect r = new Rect(x + 10, y + 20 + i * 20, 600, 20);
            string text = idx + ": " + it.id + " (" + it.fileName + ") [" + it.channel + "]";

            if (idx == _index)
            {
                GUI.Label(r, "▶ " + text);
            }
            else
            {
                if (GUI.Button(r, text))
                {
                    _index = idx;
                    PlaySelected();
                }
            }
        }
    }

    private void BuildList()
    {
        if (database == null)
        {
            _entries = new List<SoundDatabaseSO.Entry>();
            return;
        }

        IReadOnlyList<SoundDatabaseSO.Entry> src = database.Entries;
        _entries = new List<SoundDatabaseSO.Entry>(src.Count);

        for (int i = 0; i < src.Count; i++)
        {
            SoundDatabaseSO.Entry e = src[i];
            if (e == null)
            {
                continue;
            }

            _entries.Add(e);
        }

        if (_entries.Count > 0)
        {
            _index = Mathf.Clamp(_index, 0, _entries.Count - 1);
        }
        else
        {
            _index = 0;
        }
    }

    private void Prev()
    {
        _index--;
        if (_index < 0)
        {
            _index = _entries.Count - 1;
        }
    }

    private void Next()
    {
        _index++;
        if (_index >= _entries.Count)
        {
            _index = 0;
        }
    }

    private void JumpByNumber(int n)
    {
        if (_entries.Count <= 0)
        {
            return;
        }

        int chunk = Mathf.Max(1, _entries.Count / 10);
        int target = n * chunk;
        if (target >= _entries.Count)
        {
            target = _entries.Count - 1;
        }

        _index = target;
        PlaySelected();
    }

    private void PlaySelected()
    {
        if (_entries == null || _entries.Count <= 0)
        {
            return;
        }

        SoundDatabaseSO.Entry e = _entries[_index];

        if (e.channel == EAudioChannel.BGM)
        {
            SoundManager.Instance.PlaySound(e.id);
            return;
        }

        SoundManager.Instance.PlaySound(e.id, sfxVolumeMul, sfxPitch);
    }

    private void StopBgm()
    {
        SoundManager.Instance.StopBgm();
    }
}
