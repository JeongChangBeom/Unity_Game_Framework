using System.Collections.Generic;
using GameFramework.Core;
using GameFramework.SaveLoad;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace GameFramework.SoundSystem
{
    public sealed class SoundManager : MonoSingleton<SoundManager>
    {
        private const string SettingsDomain = "settings";
        private const string SoundSettingsKey = "sound";

        private SoundManagerSettings _settings;
        private SoundDatabaseSO _database;
        private SoundSettingsData _volumeSettings;
        private SoundPlayerPool _pool;

        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

        private AudioSource _bgmSource;
        private ESound _currentBgm = ESound.None;
        private int _bgmPlayToken;

        private int _voiceActiveCount;
        private float _duckTargetMultiplier = 1f;
        private float _duckCurrentMultiplier = 1f;

        protected override void OnInitialize()
        {
            _settings = LoadSettings();
            _database = LoadDatabase();

            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(SoundSettingsKey);
            _volumeSettings = SaveManager.Instance.LoadOrCreate(key, () => new SoundSettingsData(), saveIfMissing: true);

            _pool = new SoundPlayerPool(transform, _settings.InitialPoolSize, _settings.MaxPoolSize);

            _bgmSource = CreateBgmSource();

            _ = PreloadSounds();
        }

        private static SoundManagerSettings LoadSettings()
        {
            SoundManagerSettings settings = Resources.Load<SoundManagerSettings>(SoundManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[SoundManager] No SoundManagerSettings asset found at Resources/{SoundManagerSettings.ResourcePath}. Using defaults. Create one via Assets/Create/Game Framework/Sound/Sound Manager Settings.");
            return ScriptableObject.CreateInstance<SoundManagerSettings>();
        }

        private static SoundDatabaseSO LoadDatabase()
        {
            SoundDatabaseSO database = Resources.Load<SoundDatabaseSO>(SoundDatabaseSO.ResourcePath);

            if (database == null)
            {
                Debug.LogError($"[SoundManager] No SoundDatabaseSO found at Resources/{SoundDatabaseSO.ResourcePath}. Build it via Game Framework/Sound/Build Sound Database From Sheet + Folder. PlaySound will do nothing until then.");
            }

            return database;
        }

        private AudioSource CreateBgmSource()
        {
            GameObject go = new GameObject("BGM_Source");
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.outputAudioMixerGroup = _settings.BgmMixerGroup;

            return source;
        }

        private async Awaitable PreloadSounds()
        {
            if (_settings.PreloadSounds == null)
            {
                return;
            }

            foreach (ESound id in _settings.PreloadSounds)
            {
                if (id == ESound.None || _database == null)
                {
                    continue;
                }

                if (!_database.TryGet(id, out SoundDatabaseSO.Entry entry))
                {
                    continue;
                }

                await LoadClip(entry.fileName);
            }
        }

        private void Update()
        {
            _pool.Tick(OnOneShotReturned);

            float duckSpeed = 1f / Mathf.Max(0.01f, _settings.DuckFadeSeconds);
            _duckCurrentMultiplier = Mathf.MoveTowards(_duckCurrentMultiplier, _duckTargetMultiplier, duckSpeed * Time.unscaledDeltaTime);

            if (_currentBgm != ESound.None && _database != null && _database.TryGet(_currentBgm, out SoundDatabaseSO.Entry bgmEntry))
            {
                _bgmSource.volume = ComputeVolume(ESoundChannel.BGM, bgmEntry.defaultVolume) * _duckCurrentMultiplier;
            }
        }

        private void OnOneShotReturned(ESound finishedId)
        {
            // This is the single authoritative place a one-shot is known to have actually
            // finished (Tick() owns the pool's IsFinished() polling) -- ending a duck here,
            // instead of a second polling loop inside PlayOneShot, avoids a race where the
            // pool could recycle the SoundPlayer for a new sound before a second poller
            // notices the original one finished.
            if (_settings.DuckBgmOnVoice && _database != null &&
                _database.TryGet(finishedId, out SoundDatabaseSO.Entry entry) &&
                entry.channel == ESoundChannel.Voice)
            {
                EndDuck();
            }
        }

        // ---- Playback ----

        public void PlaySound(ESound id)
        {
            if (id == ESound.None || _database == null)
            {
                return;
            }

            if (!_database.TryGet(id, out SoundDatabaseSO.Entry entry))
            {
                Debug.LogWarning($"[SoundManager] No SoundDatabaseSO entry for {id}.");
                return;
            }

            if (entry.channel == ESoundChannel.BGM)
            {
                _ = PlayBgm(id, entry);
            }
            else
            {
                _ = PlayOneShot(id, entry);
            }
        }

        private async Awaitable PlayBgm(ESound id, SoundDatabaseSO.Entry entry)
        {
            if (_currentBgm == id)
            {
                return;
            }

            int myToken = ++_bgmPlayToken;

            AudioClip clip = await LoadClip(entry.fileName);
            if (clip == null || myToken != _bgmPlayToken)
            {
                return;
            }

            await FadeVolume(_bgmSource, _bgmSource.volume, 0f, _settings.BgmCrossfadeSeconds);
            if (myToken != _bgmPlayToken)
            {
                return;
            }

            _bgmSource.clip = clip;
            _bgmSource.loop = entry.loop;
            _bgmSource.Play();
            _currentBgm = id;

            float target = ComputeVolume(ESoundChannel.BGM, entry.defaultVolume) * _duckCurrentMultiplier;
            await FadeVolume(_bgmSource, 0f, target, _settings.BgmCrossfadeSeconds);
        }

        public void StopBgm()
        {
            _bgmPlayToken++;
            _bgmSource.Stop();
            _bgmSource.clip = null;
            _currentBgm = ESound.None;
        }

        private async Awaitable PlayOneShot(ESound id, SoundDatabaseSO.Entry entry)
        {
            if (CountActive(id) >= Mathf.Max(1, entry.maxConcurrent))
            {
                return;
            }

            AudioClip clip = await LoadClip(entry.fileName);
            if (clip == null)
            {
                return;
            }

            SoundPlayer player = _pool.Rent();
            if (player == null)
            {
                return;
            }

            player.SetOutputGroup(GetMixerGroup(entry.channel));

            if (entry.channel == ESoundChannel.Voice && _settings.DuckBgmOnVoice)
            {
                BeginDuck();
            }

            player.Play(id, entry.channel, clip, ComputeVolume(entry.channel, entry.defaultVolume), 1f, entry.loop);
        }

        /// <summary>Stops every currently-playing instance of this sound (BGM or one-shot).</summary>
        public void StopSound(ESound id)
        {
            if (id == ESound.None)
            {
                return;
            }

            if (_currentBgm == id)
            {
                StopBgm();
            }

            IReadOnlyList<SoundPlayer> active = _pool.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                SoundPlayer p = active[i];
                if (p != null && p.CurrentSound == id)
                {
                    _pool.Release(p);
                }
            }
        }

        public void StopAllOneShots()
        {
            _pool.StopAll();
        }

        // ---- Volume ----

        public void SetMasterVolume(float volume)
        {
            _volumeSettings.master = Mathf.Clamp01(volume);
            ApplyVolumesToActivePlayers();
            SaveVolumeSettings();
        }

        public void SetChannelVolume(ESoundChannel channel, float volume)
        {
            _volumeSettings.Set(channel, volume);
            ApplyVolumesToActivePlayers();
            SaveVolumeSettings();
        }

        public float GetChannelVolume(ESoundChannel channel)
        {
            return _volumeSettings.Get(channel);
        }

        public float GetMasterVolume()
        {
            return _volumeSettings.master;
        }

        private void ApplyVolumesToActivePlayers()
        {
            IReadOnlyList<SoundPlayer> active = _pool.Active;
            for (int i = 0; i < active.Count; i++)
            {
                SoundPlayer p = active[i];
                if (p == null || !_database.TryGet(p.CurrentSound, out SoundDatabaseSO.Entry entry))
                {
                    continue;
                }

                p.Source.volume = ComputeVolume(p.CurrentChannel, entry.defaultVolume);
            }
        }

        private void SaveVolumeSettings()
        {
            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(SoundSettingsKey);
            SaveManager.Instance.Save(key, _volumeSettings);
            SaveManager.Instance.Flush();
        }

        private float ComputeVolume(ESoundChannel channel, float entryDefaultVolume)
        {
            return _volumeSettings.master * _volumeSettings.Get(channel) * entryDefaultVolume;
        }

        // ---- Ducking ----

        private void BeginDuck()
        {
            _voiceActiveCount++;
            _duckTargetMultiplier = _settings.DuckedBgmVolumeScale;
        }

        private void EndDuck()
        {
            _voiceActiveCount = Mathf.Max(0, _voiceActiveCount - 1);

            if (_voiceActiveCount == 0)
            {
                _duckTargetMultiplier = 1f;
            }
        }

        // ---- Helpers ----

        private int CountActive(ESound id)
        {
            IReadOnlyList<SoundPlayer> active = _pool.Active;
            int count = 0;

            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].CurrentSound == id)
                {
                    count++;
                }
            }

            return count;
        }

        private AudioMixerGroup GetMixerGroup(ESoundChannel channel)
        {
            switch (channel)
            {
                case ESoundChannel.BGM: return _settings.BgmMixerGroup;
                case ESoundChannel.SFX: return _settings.SfxMixerGroup;
                case ESoundChannel.UI: return _settings.UiMixerGroup;
                case ESoundChannel.Voice: return _settings.VoiceMixerGroup;
                default: return null;
            }
        }

        private static async Awaitable FadeVolume(AudioSource source, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                source.volume = to;
                return;
            }

            float time = 0f;
            source.volume = from;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, to, time / duration);
                await Awaitable.NextFrameAsync();
            }

            source.volume = to;
        }

        private async Awaitable<AudioClip> LoadClip(string fileName)
        {
            if (_clipCache.TryGetValue(fileName, out AudioClip cached))
            {
                return cached;
            }

            var handle = Addressables.LoadAssetAsync<AudioClip>(fileName);
            AudioClip clip = await handle.Task;

            if (clip != null)
            {
                _clipCache[fileName] = clip;
            }

            return clip;
        }
    }
}
