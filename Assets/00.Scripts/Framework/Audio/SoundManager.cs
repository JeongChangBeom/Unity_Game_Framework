using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using GameFramework.Core;
using GameFramework.SaveLoad;

public class SoundManager : MonoSingleton<SoundManager>
{
    private const string KEY_BGM_VOLUME = "sound/bgm_volume";
    private const string KEY_SFX_VOLUME = "sound/sfx_volume";

    private SaveManager _save;

    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Pool")]
    [SerializeField] private int initialPoolSize = 10;

    private float _bgmVolume = 1f;
    private float _sfxVolume = 1f;

    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, int> _playingCount = new Dictionary<string, int>();

    private readonly Queue<AudioSource> _pool = new Queue<AudioSource>();

    private AudioSource _bgmSource;

    protected override void OnInitialize()
    {
        _save = SaveManager.Instance;

        LoadVolume();
        ApplyVolume();

        InitPool();
        InitBGMSource();
    }

    private void InitPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            AudioSource source = CreateSource("SFX_Source");
            _pool.Enqueue(source);
        }
    }

    private void InitBGMSource()
    {
        _bgmSource = CreateSource("BGM_Source");
        _bgmSource.loop = true;
    }

    private AudioSource CreateSource(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;

        return source;
    }


    private void LoadVolume()
    {
        _bgmVolume = _save.LoadOrCreate(KEY_BGM_VOLUME, () => 1f, saveIfMissing: true);
        _sfxVolume = _save.LoadOrCreate(KEY_SFX_VOLUME, () => 1f, saveIfMissing: true);
    }

    private void SaveVolume()
    {
        _save.Save(KEY_BGM_VOLUME, _bgmVolume);
        _save.Save(KEY_SFX_VOLUME, _sfxVolume);
        _save.Flush();
    }

    private void ApplyVolume()
    {
        audioMixer.SetFloat("BGM", LinearToDb(_bgmVolume));
        audioMixer.SetFloat("SFX", LinearToDb(_sfxVolume));
    }

    public void SetBGMVolume(float volume)
    {
        _bgmVolume = volume;
        ApplyVolume();
        SaveVolume();
    }

    public void SetSFXVolume(float volume)
    {
        _sfxVolume = volume;
        ApplyVolume();
        SaveVolume();
    }

    private float LinearToDb(float value)
    {
        if (value <= 0.0001f) return -80f;
        return Mathf.Log10(value) * 20f;
    }

    public void PlaySound(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (key.StartsWith("BGM_"))
        {
            PlayBGM(key).Forget();
        }
        else
        {
            PlaySFX(key).Forget();
        }
    }

    private async UniTaskVoid PlayBGM(string key)
    {
        AudioClip clip = await LoadClip(key);

        if (clip == null)
        {
            return;
        }

        if (_bgmSource.clip == clip)
        {
            return;
        }

        await FadeOut(_bgmSource, 0.5f);

        _bgmSource.clip = clip;
        _bgmSource.Play();

        await FadeIn(_bgmSource, 0.5f);
    }

    private async UniTaskVoid PlaySFX(string key)
    {
        if (!TryIncreasePlayingCount(key, 3))
        {
            return;
        }

        AudioClip clip = await LoadClip(key);

        if (clip == null)
        {
            DecreasePlayingCount(key);
            return;
        }

        AudioSource source = GetSource();
        source.clip = clip;
        source.Play();

        await UniTask.WaitWhile(() => source.isPlaying);

        ReturnSource(source);
        DecreasePlayingCount(key);
    }

    private AudioSource GetSource()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }

        return CreateSource("SFX_Extra");
    }

    private void ReturnSource(AudioSource source)
    {
        source.Stop();
        source.clip = null;
        _pool.Enqueue(source);
    }
    private async UniTask<AudioClip> LoadClip(string key)
    {
        AudioClip clip;

        bool exists = _clipCache.TryGetValue(key, out clip);

        if (exists)
        {
            return clip;
        }

        clip = await Addressables.LoadAssetAsync<AudioClip>(key);

        if (clip != null)
        {
            _clipCache.Add(key, clip);
        }

        return clip;
    }

    private bool TryIncreasePlayingCount(string key, int max)
    {
        int count;
        bool exists = _playingCount.TryGetValue(key, out count);

        if (!exists)
        {
            count = 0;
        }

        if (count >= max)
        {
            return false;
        }

        _playingCount[key] = count + 1;
        return true;
    }

    private void DecreasePlayingCount(string key)
    {
        int count;
        bool exists = _playingCount.TryGetValue(key, out count);

        if (!exists)
        {
            return;
        }

        count--;

        if (count <= 0)
        {
            _playingCount.Remove(key);
        }
        else
        {
            _playingCount[key] = count;
        }
    }

    private async UniTask FadeIn(AudioSource source, float duration)
    {
        float time = 0f;
        source.volume = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            source.volume = time / duration;
            await UniTask.Yield();
        }

        source.volume = 1f;
    }

    private async UniTask FadeOut(AudioSource source, float duration)
    {
        float start = source.volume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            source.volume = Mathf.Lerp(start, 0f, time / duration);
            await UniTask.Yield();
        }

        source.Stop();
    }
}