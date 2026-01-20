using System;
using System.Collections.Generic;
using UnityEngine;

public class SoundPlayTester : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private SoundDatabaseSO database;

    [Header("Play")]
    [SerializeField] private bool playOnStart;
    [SerializeField] private int startIndex;
    [SerializeField] private float sfxVolumeMul = 1f;
    [SerializeField] private float sfxPitch = 1f;

    [Header("Mixer Volumes (Saved)")]
    [SerializeField] private float masterVolume = 1f;
    [SerializeField] private float bgmVolume = 1f;
    [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private float uiVolume = 1f;
    [SerializeField] private float voiceVolume = 1f;

    private readonly List<Item> _items = new List<Item>();
    private int _index;

    private struct Item
    {
        public SoundDatabaseSO.Entry entry;
        public ESound sound;
        public bool valid;
    }

    private void Start()
    {
        BuildList();

        if (_items.Count <= 0)
        {
            Debug.LogError("[SoundPlayTester] No valid entries. Assign SoundDatabaseSO and run Build Sound Database first. " +
                           "Also ensure entry.id matches ESound enum name.");
            return;
        }

        _index = Mathf.Clamp(startIndex, 0, _items.Count - 1);

        SyncFromSoundManager();

        if (playOnStart)
        {
            PlaySelected();
        }
    }

    private void Update()
    {
        if (_items.Count <= 0)
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

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
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

        if (Input.GetKeyDown(KeyCode.F))
        {
            ForceFlush();
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
        if (_items.Count <= 0)
        {
            GUI.Label(new Rect(10, 10, 900, 25),
                "SoundPlayTester: No valid entries. Assign SoundDatabaseSO and ensure entry.id == ESound name.");
            return;
        }

        int x = 10;
        int y = 10;

        GUI.Label(new Rect(x, y, 1000, 25),
            "↑/↓: Select | Enter/Space: Play | `: Stop BGM | R: Reload DB | 0~9: Jump | F: Flush Save");

        y += 30;

        Item it = _items[_index];
        SoundDatabaseSO.Entry e = it.entry;

        GUI.Label(new Rect(x, y, 1200, 25),
            "Selected [" + _index + "/" + (_items.Count - 1) + "] : " +
            it.sound + " / " + e.fileName + " / " + e.channel);

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

        if (GUI.Button(new Rect(x + 600, y, 140, 30), "Flush Save"))
        {
            ForceFlush();
        }

        y += 45;

        GUI.Label(new Rect(x, y, 300, 25), "SFX VolumeMul: " + sfxVolumeMul.ToString("0.00"));
        sfxVolumeMul = GUI.HorizontalSlider(new Rect(x + 140, y + 8, 200, 20), sfxVolumeMul, 0f, 2f);

        y += 25;

        GUI.Label(new Rect(x, y, 300, 25), "SFX Pitch: " + sfxPitch.ToString("0.00"));
        sfxPitch = GUI.HorizontalSlider(new Rect(x + 140, y + 8, 200, 20), sfxPitch, 0.5f, 2f);

        y += 35;

        GUI.Box(new Rect(x, y, 620, 165), "Saved Mixer Volumes (change -> saved via SoundManager)");
        y += 25;

        DrawSavedVolumeSlider(x, ref y, "Master", ref masterVolume, 0f, 1f, ApplyMaster);
        DrawSavedVolumeSlider(x, ref y, "BGM", ref bgmVolume, 0f, 1f, ApplyBgm);
        DrawSavedVolumeSlider(x, ref y, "SFX", ref sfxVolume, 0f, 1f, ApplySfx);
        DrawSavedVolumeSlider(x, ref y, "UI", ref uiVolume, 0f, 1f, ApplyUi);
        DrawSavedVolumeSlider(x, ref y, "Voice", ref voiceVolume, 0f, 1f, ApplyVoice);

        y += 10;

        int listHeight = Mathf.Min(18, _items.Count) * 20;
        GUI.Box(new Rect(x, y, 620, listHeight + 10), "Entries (top 18)");

        int showCount = Mathf.Min(18, _items.Count);
        int start = Mathf.Clamp(_index - showCount / 2, 0, Mathf.Max(0, _items.Count - showCount));

        for (int i = 0; i < showCount; i++)
        {
            int idx = start + i;
            Item row = _items[idx];
            SoundDatabaseSO.Entry rowEntry = row.entry;

            Rect r = new Rect(x + 10, y + 20 + i * 20, 600, 20);
            string text = idx + ": " + row.sound + " (" + rowEntry.fileName + ") [" + rowEntry.channel + "]";

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

    private void DrawSavedVolumeSlider(
        int x,
        ref int y,
        string label,
        ref float value,
        float min,
        float max,
        Action<float> onChanged
    )
    {
        GUI.Label(new Rect(x + 10, y, 200, 25), label + ": " + value.ToString("0.00"));

        float next = GUI.HorizontalSlider(new Rect(x + 140, y + 8, 200, 20), value, min, max);

        if (Mathf.Abs(next - value) > 0.0001f)
        {
            value = next;
            if (onChanged != null)
            {
                onChanged(value);
            }
        }

        y += 25;
    }

    private void SyncFromSoundManager()
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        masterVolume = SoundManager.Instance.GetMasterVolume();
        bgmVolume = SoundManager.Instance.GetChannelVolume(EAudioChannel.BGM);
        sfxVolume = SoundManager.Instance.GetChannelVolume(EAudioChannel.SFX);
        uiVolume = SoundManager.Instance.GetChannelVolume(EAudioChannel.UI);
        voiceVolume = SoundManager.Instance.GetChannelVolume(EAudioChannel.Voice);
    }

    private void ApplyMaster(float v)
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.SetMasterVolume(v);
    }

    private void ApplyBgm(float v)
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.SetChannelVolume(EAudioChannel.BGM, v);
    }

    private void ApplySfx(float v)
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.SetChannelVolume(EAudioChannel.SFX, v);
    }

    private void ApplyUi(float v)
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.SetChannelVolume(EAudioChannel.UI, v);
    }

    private void ApplyVoice(float v)
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.SetChannelVolume(EAudioChannel.Voice, v);
    }

    private void ForceFlush()
    {
        if (SaveManager.Instance == null)
        {
            return;
        }

        SaveManager.Instance.Flush();
    }

    private void BuildList()
    {
        _items.Clear();

        if (database == null)
        {
            Debug.LogWarning("[SoundPlayTester] database is null. Please assign SoundDatabaseSO.");
            _index = 0;
            return;
        }

        IReadOnlyList<SoundDatabaseSO.Entry> src = database.Entries;
        if (src == null)
        {
            Debug.LogWarning("[SoundPlayTester] database.Entries is null.");
            _index = 0;
            return;
        }

        int invalidIdCount = 0;

        for (int i = 0; i < src.Count; i++)
        {
            SoundDatabaseSO.Entry e = src[i];
            if (e == null)
            {
                continue;
            }

            if (e.id == ESound.None)
            {
                continue;
            }

            Item item = new Item();
            item.entry = e;
            item.sound = e.id;
            item.valid = true;

            _items.Add(item);
        }


        if (invalidIdCount > 0)
        {
            Debug.LogWarning("[SoundPlayTester] Skipped " + invalidIdCount +
                             " entries because entry.id could not be parsed to ESound. " +
                             "Ensure entry.id exactly matches the ESound enum name.");
        }

        if (_items.Count > 0)
        {
            _index = Mathf.Clamp(_index, 0, _items.Count - 1);
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
            _index = _items.Count - 1;
        }
    }

    private void Next()
    {
        _index++;
        if (_index >= _items.Count)
        {
            _index = 0;
        }
    }

    private void JumpByNumber(int n)
    {
        if (_items.Count <= 0)
        {
            return;
        }

        int chunk = Mathf.Max(1, _items.Count / 10);
        int target = n * chunk;
        if (target >= _items.Count)
        {
            target = _items.Count - 1;
        }

        _index = target;
        PlaySelected();
    }

    private void PlaySelected()
    {
        if (_items.Count <= 0)
        {
            return;
        }

        if (SoundManager.Instance == null)
        {
            Debug.LogError("[SoundPlayTester] SoundManager.Instance is null.");
            return;
        }

        Item it = _items[_index];
        SoundDatabaseSO.Entry e = it.entry;

        if (e == null)
        {
            return;
        }

        if (e.channel == EAudioChannel.BGM)
        {
            SoundManager.Instance.PlaySound(it.sound);
            return;
        }

        SoundManager.Instance.PlaySound(it.sound, sfxVolumeMul, sfxPitch);
    }

    private void StopBgm()
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.StopBgm();
    }
}
