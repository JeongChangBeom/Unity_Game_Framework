using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SoundManager : MonoSingleton<SoundManager>
{
    [Header("Database")]
    [SerializeField] private SoundDatabaseSO database;

    [Header("Auto Assign (Resources)")]
    [SerializeField] private string resourcesDatabasePath = "SoundDatabaseSO";
    [SerializeField] private string resourcesDatabasePathFallback = "Audio/SoundDatabaseSO";

    [Header("Mixer (Optional)")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterParam = "Master";
    [SerializeField] private string bgmParam = "BGM";
    [SerializeField] private string sfxParam = "SFX";
    [SerializeField] private string uiParam = "UI";
    [SerializeField] private string voiceParam = "Voice";

    [Header("Pool")]
    [SerializeField] private int initialPoolCount = 10;
    [SerializeField] private int maxPoolCount = 30;

    [Header("BGM")]
    [SerializeField] private float bgmFadeSeconds = 0.8f;

    private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _clipHandles = new Dictionary<string, AsyncOperationHandle<AudioClip>>();
    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

    private readonly Dictionary<ESound, int> _playingCountBySound = new Dictionary<ESound, int>();

    private AudioSource _bgmA;
    private AudioSource _bgmB;
    private AudioSource _currentBgm;
    private AudioSource _nextBgm;

    private AudioSettingsData _settings = new AudioSettingsData();
    private const string SAVE_KEY = "AudioSettings";

    private SoundPlayerPool _playerPool;

    private bool _databaseResolved;
    private bool _databaseMissingLogged;

    protected override void OnInitialize()
    {
        ResolveDatabaseOrError();
        SetupBgmSources();
        _playerPool = new SoundPlayerPool(transform, initialPoolCount, maxPoolCount);
        LoadSettings();
        ApplyMixerVolumes();
    }

    private void Update()
    {
        if (_playerPool != null)
        {
            _playerPool.Tick(DecreaseCount);
        }
    }

    public void PlaySound(ESound sound, float volumeMul = 1f, float pitch = 1f)
    {
        if (sound == ESound.None)
        {
            return;
        }

        if (database == null)
        {
            ResolveDatabaseOrError();
        }

        if (database == null)
        {
            return;
        }

        SoundDatabaseSO.Entry entry;
        bool found = database.TryGet(sound, out entry);
        if (found == false || entry == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(entry.fileName))
        {
            return;
        }

        if (entry.channel == EAudioChannel.BGM)
        {
            PlayBgm(entry);
            return;
        }

        if (CanPlay(sound, entry.maxConcurrent) == false)
        {
            return;
        }

        string address = entry.fileName;

        AudioClip cached = GetCachedClip(address);
        if (cached != null)
        {
            PlayOneShotInternal(sound, entry, cached, volumeMul, pitch);
            return;
        }

        LoadClipAsync(address, (loaded) =>
        {
            if (loaded == null)
            {
                DecreaseCount(sound);
                return;
            }

            PlayOneShotInternal(sound, entry, loaded, volumeMul, pitch);
        });
    }

    public void StopBgm(float fadeSeconds = -1f)
    {
        float t = fadeSeconds;
        if (t < 0f)
        {
            t = bgmFadeSeconds;
        }

        if (_currentBgm != null && _currentBgm.isPlaying)
        {
            StartCoroutine(FadeOutCoroutine(_currentBgm, t, true));
        }

        if (_nextBgm != null && _nextBgm.isPlaying)
        {
            StartCoroutine(FadeOutCoroutine(_nextBgm, t, true));
        }
    }

    public void StopAllOneShots()
    {
        if (_playerPool != null)
        {
            _playerPool.StopAll();
        }

        _playingCountBySound.Clear();
    }

    public void SetMasterVolume(float v)
    {
        _settings.master = Mathf.Clamp01(v);
        ApplyMixerVolumes();
        SaveSettings();
    }

    public void SetChannelVolume(EAudioChannel channel, float v)
    {
        _settings.Set(channel, v);
        ApplyMixerVolumes();
        SaveSettings();
    }

    public float GetMasterVolume()
    {
        return _settings.master;
    }

    public float GetChannelVolume(EAudioChannel channel)
    {
        return _settings.Get(channel);
    }

    public void ReleaseAllCachedClips()
    {
        foreach (KeyValuePair<string, AsyncOperationHandle<AudioClip>> kv in _clipHandles)
        {
            AsyncOperationHandle<AudioClip> h = kv.Value;
            if (h.IsValid())
            {
                Addressables.Release(h);
            }
        }

        _clipHandles.Clear();
        _clipCache.Clear();
    }

    private void ResolveDatabaseOrError()
    {
        if (_databaseResolved)
        {
            return;
        }

        _databaseResolved = true;

        if (database != null)
        {
            return;
        }

        string p0 = resourcesDatabasePath;
        if (string.IsNullOrEmpty(p0))
        {
            p0 = "SoundDatabase";
        }

        SoundDatabaseSO a = Resources.Load<SoundDatabaseSO>(p0);
        if (a != null)
        {
            database = a;
            return;
        }

        string p1 = resourcesDatabasePathFallback;
        if (string.IsNullOrEmpty(p1))
        {
            p1 = "Audio/SoundDatabase";
        }

        SoundDatabaseSO b = Resources.Load<SoundDatabaseSO>(p1);
        if (b != null)
        {
            database = b;
            return;
        }

        LogDatabaseMissingOnce(p0, p1);
    }

    private void LogDatabaseMissingOnce(string p0, string p1)
    {
        if (_databaseMissingLogged)
        {
            return;
        }

        _databaseMissingLogged = true;

        Debug.LogError(
            "[SoundManager] SoundDatabaseSO not found.\n" +
            "Required: put a SoundDatabaseSO asset at one of the following paths:\n" +
            "1) Assets/Resources/" + p0 + ".asset\n" +
            "2) Assets/Resources/" + p1 + ".asset\n" +
            "Then run: Framework/Audio/Build Sound Database From Sheet + Folder");
    }

    private void PlayBgm(SoundDatabaseSO.Entry entry)
    {
        string address = entry.fileName;

        AudioClip cached = GetCachedClip(address);
        if (cached != null)
        {
            CrossFadeBgm(cached);
            return;
        }

        LoadClipAsync(address, (loaded) =>
        {
            if (loaded == null)
            {
                return;
            }

            CrossFadeBgm(loaded);
        });
    }

    private void CrossFadeBgm(AudioClip nextClip)
    {
        if (nextClip == null)
        {
            return;
        }

        if (_currentBgm != null && _currentBgm.clip == nextClip && _currentBgm.isPlaying)
        {
            return;
        }

        _nextBgm.clip = nextClip;
        _nextBgm.volume = 0f;
        _nextBgm.loop = true;
        _nextBgm.Play();

        float t = bgmFadeSeconds;

        if (_currentBgm != null && _currentBgm.isPlaying)
        {
            StartCoroutine(FadeOutCoroutine(_currentBgm, t, true));
        }

        StartCoroutine(FadeInCoroutine(_nextBgm, t, 1f));

        AudioSource temp = _currentBgm;
        _currentBgm = _nextBgm;
        _nextBgm = temp;
    }

    private void PlayOneShotInternal(ESound sound, SoundDatabaseSO.Entry entry, AudioClip clip, float volumeMul, float pitch)
    {
        if (_playerPool == null)
        {
            DecreaseCount(sound);
            return;
        }

        SoundPlayer player = _playerPool.Rent();
        if (player == null)
        {
            DecreaseCount(sound);
            return;
        }

        float channelVol = _settings.Get(entry.channel);
        float finalVol = entry.defaultVolume * volumeMul * channelVol * _settings.master;

        player.Play(sound, clip, finalVol, pitch, entry.loop);
    }

    private bool CanPlay(ESound sound, int maxConcurrent)
    {
        int limit = maxConcurrent;
        if (limit <= 0)
        {
            limit = 1;
        }

        int count = 0;
        if (_playingCountBySound.ContainsKey(sound))
        {
            count = _playingCountBySound[sound];
        }

        if (count >= limit)
        {
            return false;
        }

        _playingCountBySound[sound] = count + 1;
        return true;
    }

    private void DecreaseCount(ESound sound)
    {
        if (_playingCountBySound.ContainsKey(sound) == false)
        {
            return;
        }

        int count = _playingCountBySound[sound];
        count--;

        if (count <= 0)
        {
            _playingCountBySound.Remove(sound);
        }
        else
        {
            _playingCountBySound[sound] = count;
        }
    }

    private void SetupBgmSources()
    {
        GameObject a = new GameObject("BGM_A");
        a.transform.SetParent(transform);
        _bgmA = a.AddComponent<AudioSource>();
        _bgmA.loop = true;
        _bgmA.playOnAwake = false;

        GameObject b = new GameObject("BGM_B");
        b.transform.SetParent(transform);
        _bgmB = b.AddComponent<AudioSource>();
        _bgmB.loop = true;
        _bgmB.playOnAwake = false;

        _currentBgm = _bgmA;
        _nextBgm = _bgmB;
    }

    private AudioClip GetCachedClip(string address)
    {
        if (_clipCache.ContainsKey(address))
        {
            return _clipCache[address];
        }

        return null;
    }

    private void LoadClipAsync(string address, System.Action<AudioClip> onDone)
    {
        if (_clipHandles.ContainsKey(address))
        {
            AsyncOperationHandle<AudioClip> handle = _clipHandles[address];

            if (handle.IsDone)
            {
                AudioClip c = handle.Result;
                CacheClip(address, c);
                onDone?.Invoke(c);
                return;
            }

            handle.Completed += (h) =>
            {
                AudioClip c = h.Result;
                CacheClip(address, c);
                onDone?.Invoke(c);
            };

            return;
        }

        AsyncOperationHandle<AudioClip> newHandle = Addressables.LoadAssetAsync<AudioClip>(address);
        _clipHandles[address] = newHandle;

        newHandle.Completed += (h) =>
        {
            AudioClip c = h.Result;
            CacheClip(address, c);
            onDone?.Invoke(c);
        };
    }

    private void CacheClip(string address, AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        _clipCache[address] = clip;
    }

    private void ApplyMixerVolumes()
    {
        if (audioMixer == null)
        {
            return;
        }

        SetMixerDb(masterParam, _settings.master);
        SetMixerDb(bgmParam, _settings.bgm);
        SetMixerDb(sfxParam, _settings.sfx);
        SetMixerDb(uiParam, _settings.ui);
        SetMixerDb(voiceParam, _settings.voice);
    }

    private void SetMixerDb(string param, float linear01)
    {
        if (string.IsNullOrEmpty(param))
        {
            return;
        }

        float v = Mathf.Clamp01(linear01);

        float db;
        if (v <= 0.0001f)
        {
            db = -80f;
        }
        else
        {
            db = Mathf.Log10(v) * 20f;
        }

        audioMixer.SetFloat(param, db);
    }

    private void LoadSettings()
    {
#if ES3
        if (ES3.KeyExists(SAVE_KEY))
        {
            _settings = ES3.Load<AudioSettingsData>(SAVE_KEY);
        }
        else
        {
            _settings = new AudioSettingsData();
        }
#else
        _settings.master = PlayerPrefs.GetFloat(SAVE_KEY + "_master", 1f);
        _settings.bgm = PlayerPrefs.GetFloat(SAVE_KEY + "_bgm", 1f);
        _settings.sfx = PlayerPrefs.GetFloat(SAVE_KEY + "_sfx", 1f);
        _settings.ui = PlayerPrefs.GetFloat(SAVE_KEY + "_ui", 1f);
        _settings.voice = PlayerPrefs.GetFloat(SAVE_KEY + "_voice", 1f);
#endif
    }

    private void SaveSettings()
    {
#if ES3
        ES3.Save(SAVE_KEY, _settings);
#else
        PlayerPrefs.SetFloat(SAVE_KEY + "_master", _settings.master);
        PlayerPrefs.SetFloat(SAVE_KEY + "_bgm", _settings.bgm);
        PlayerPrefs.SetFloat(SAVE_KEY + "_sfx", _settings.sfx);
        PlayerPrefs.SetFloat(SAVE_KEY + "_ui", _settings.ui);
        PlayerPrefs.SetFloat(SAVE_KEY + "_voice", _settings.voice);
        PlayerPrefs.Save();
#endif
    }

    private IEnumerator FadeOutCoroutine(AudioSource src, float seconds, bool stopAfter)
    {
        if (src == null)
        {
            yield break;
        }

        float start = src.volume;
        float t = 0f;

        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float n = t / seconds;
            src.volume = Mathf.Lerp(start, 0f, n);
            yield return null;
        }

        src.volume = 0f;

        if (stopAfter)
        {
            src.Stop();
            src.clip = null;
        }
    }

    private IEnumerator FadeInCoroutine(AudioSource src, float seconds, float targetVolume)
    {
        if (src == null)
        {
            yield break;
        }

        float start = src.volume;
        float t = 0f;

        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float n = t / seconds;
            src.volume = Mathf.Lerp(start, targetVolume, n);
            yield return null;
        }

        src.volume = targetVolume;
    }
}
