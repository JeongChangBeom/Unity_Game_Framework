using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using GameFramework.Core;
using GameFramework.SaveLoad;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameFramework.SoundSystem
{
    public sealed class SoundManager : MonoSingleton<SoundManager>
    {
        private struct SoundEntry
        {
            public ESoundChannel channel;
            public string fileName;
            public float defaultVolume;
            public int maxConcurrent;
            public bool loop;
        }

        private const string SettingsDomain = "settings";
        private const string SoundSettingsKey = "sound";

        private SoundManagerSettings _settings;
        private Dictionary<string, SoundEntry> _soundData;
        private SoundSettingsData _volumeSettings;
        private SoundPlayerPool _pool;

        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _clipHandles = new Dictionary<string, AsyncOperationHandle<AudioClip>>();
        private readonly Dictionary<string, Task<AudioClip>> _pendingClipLoads = new Dictionary<string, Task<AudioClip>>();
        private readonly Dictionary<string, int> _pendingOneShotCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _oneShotStopTokens = new Dictionary<string, int>();

        private bool _isShuttingDown;

        private AudioSource _bgmSource;
        private string _currentBgm;
        private string _requestedBgm;
        private int _bgmPlayToken;
        private bool _bgmCrossfading;

        private int _voiceActiveCount;
        private float _duckTargetMultiplier = 1f;
        private float _duckCurrentMultiplier = 1f;

        protected override void OnInitialize()
        {
            _settings = LoadSettings();
            _soundData = LoadSoundData(_settings.SoundTableResourcePath);

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

            Debug.LogWarning($"[SoundManager] Resources/{SoundManagerSettings.ResourcePath}에서 SoundManagerSettings 에셋을 찾지 못했습니다. 기본값을 사용합니다. Assets/Create/Game Framework/Sound System/Sound Manager Settings로 에셋을 만드세요.");
            return ScriptableObject.CreateInstance<SoundManagerSettings>();
        }

        private static Dictionary<string, SoundEntry> LoadSoundData(string resourcePath)
        {
            Dictionary<string, SoundEntry> data = new Dictionary<string, SoundEntry>();

            ScriptableObject table = Resources.Load<ScriptableObject>(resourcePath);
            if (table == null)
            {
                Debug.LogError($"[SoundManager] Resources/{resourcePath}에서 Sound 테이블을 찾지 못했습니다. Data Parsing으로 Sound 시트를 생성했는지 확인하세요. 생성 전까지 PlaySound는 아무 동작도 하지 않습니다.");
                return data;
            }

            Type tableType = table.GetType();
            PropertyInfo tableProp = tableType.GetProperty("Table", BindingFlags.Public | BindingFlags.Instance);

            if (tableProp == null || !(tableProp.GetValue(table) is IEnumerable rows))
            {
                Debug.LogError($"[SoundManager] {tableType.Name}에서 Table 프로퍼티를 찾지 못했습니다.");
                return data;
            }

            foreach (object row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                Type rowType = row.GetType();

                string fileName = GetFieldValue<string>(rowType, row, "fileName");
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                string channelRaw = GetFieldValue<string>(rowType, row, "channel");

                data[fileName] = new SoundEntry
                {
                    channel = ParseChannel(channelRaw),
                    fileName = fileName,
                    defaultVolume = GetFieldValue<float>(rowType, row, "defaultVolume"),
                    maxConcurrent = GetFieldValue<int>(rowType, row, "maxConcurrent"),
                    loop = GetFieldValue<bool>(rowType, row, "loop"),
                };
            }

            return data;
        }

        private static T GetFieldValue<T>(Type type, object instance, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

            if (field == null || !(field.GetValue(instance) is T value))
            {
                return default;
            }

            return value;
        }

        private static ESoundChannel ParseChannel(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return ESoundChannel.SFX;
            }

            if (raw.Equals("BGM", StringComparison.OrdinalIgnoreCase)) return ESoundChannel.BGM;
            if (raw.Equals("SFX", StringComparison.OrdinalIgnoreCase)) return ESoundChannel.SFX;
            if (raw.Equals("UI", StringComparison.OrdinalIgnoreCase)) return ESoundChannel.UI;
            if (raw.Equals("Voice", StringComparison.OrdinalIgnoreCase)) return ESoundChannel.Voice;

            return ESoundChannel.SFX;
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

            foreach (string id in _settings.PreloadSounds)
            {
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (!_soundData.TryGetValue(id, out SoundEntry entry))
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

            if (!_bgmCrossfading && _currentBgm != null && _soundData.TryGetValue(_currentBgm, out SoundEntry bgmEntry))
            {
                _bgmSource.volume = ComputeVolume(ESoundChannel.BGM, bgmEntry.defaultVolume) * _duckCurrentMultiplier;
            }
        }

        private void OnOneShotReturned(string finishedId)
        {
            if (_settings.DuckBgmOnVoice &&
                _soundData.TryGetValue(finishedId, out SoundEntry entry) &&
                entry.channel == ESoundChannel.Voice)
            {
                EndDuck();
            }
        }

        // ---- 재생 ----

        /// <summary>id(Sound 테이블의 FileName)로 사운드를 재생합니다. Channel이 BGM이면
        /// 자동으로 크로스페이드 전환되고, 그 외에는 원샷으로 재생됩니다.</summary>
        public void PlaySound(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (!_soundData.TryGetValue(id, out SoundEntry entry))
            {
                Debug.LogWarning($"[SoundManager] {id}에 대한 Sound 데이터가 없습니다.");
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

        private async Awaitable PlayBgm(string id, SoundEntry entry)
        {
            if (_requestedBgm == id)
            {
                return;
            }

            int myToken = ++_bgmPlayToken;
            _requestedBgm = id;

            bool crossfadeStartedByMe = false;

            try
            {
                AudioClip clip = await LoadClip(entry.fileName);

                if (myToken != _bgmPlayToken)
                {
                    return;
                }

                if (clip == null)
                {
                    _requestedBgm = _currentBgm;
                    return;
                }

                _bgmCrossfading = true;
                crossfadeStartedByMe = true;

                await FadeVolume(_bgmSource, _bgmSource.volume, 0f, _settings.BgmCrossfadeSeconds, myToken);
                if (myToken != _bgmPlayToken)
                {
                    return;
                }

                _bgmSource.clip = clip;
                _bgmSource.loop = entry.loop;
                _bgmSource.Play();
                _currentBgm = id;

                float target = ComputeVolume(ESoundChannel.BGM, entry.defaultVolume) * _duckCurrentMultiplier;
                await FadeVolume(_bgmSource, 0f, target, _settings.BgmCrossfadeSeconds, myToken);
            }
            finally
            {
                if (crossfadeStartedByMe && myToken == _bgmPlayToken)
                {
                    _bgmCrossfading = false;
                }
            }
        }

        /// <summary>BGM을 정지합니다.</summary>
        public void StopBgm()
        {
            _bgmPlayToken++;
            _requestedBgm = null;
            _bgmSource.Stop();
            _bgmSource.clip = null;
            _currentBgm = null;
        }

        private async Awaitable PlayOneShot(string id, SoundEntry entry)
        {
            int maxConcurrent = Mathf.Max(1, entry.maxConcurrent);
            int pending = _pendingOneShotCounts.TryGetValue(id, out int p) ? p : 0;

            if (CountActive(id) + pending >= maxConcurrent)
            {
                return;
            }

            _pendingOneShotCounts[id] = pending + 1;
            int myStopToken = _oneShotStopTokens.TryGetValue(id, out int st) ? st : 0;

            try
            {
                AudioClip clip = await LoadClip(entry.fileName);
                if (clip == null)
                {
                    return;
                }

                int currentStopToken = _oneShotStopTokens.TryGetValue(id, out int cur2) ? cur2 : 0;
                if (currentStopToken != myStopToken)
                {
                    return;
                }

                SoundPlayer player = _pool.Rent();
                if (player == null)
                {
                    Debug.LogWarning($"[SoundManager] {id} 재생 실패: SoundPlayerPool이 가득 찼습니다 (MaxPoolSize 확인).");
                    return;
                }

                player.SetOutputGroup(GetMixerGroup(entry.channel));

                if (entry.channel == ESoundChannel.Voice && _settings.DuckBgmOnVoice)
                {
                    BeginDuck();
                }

                player.Play(id, entry.channel, clip, ComputeVolume(entry.channel, entry.defaultVolume), 1f, entry.loop);
            }
            finally
            {
                int remaining = (_pendingOneShotCounts.TryGetValue(id, out int cur) ? cur : 1) - 1;

                if (remaining <= 0)
                {
                    _pendingOneShotCounts.Remove(id);
                }
                else
                {
                    _pendingOneShotCounts[id] = remaining;
                }
            }
        }

        /// <summary>이 사운드가 현재 재생 중인 모든 인스턴스를 정지합니다 (BGM 또는 원샷).</summary>
        public void StopSound(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            _oneShotStopTokens[id] = (_oneShotStopTokens.TryGetValue(id, out int t) ? t : 0) + 1;

            if (_requestedBgm == id)
            {
                StopBgm();
            }

            IReadOnlyList<SoundPlayer> active = _pool.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                SoundPlayer p = active[i];
                if (p != null && p.CurrentSound == id)
                {
                    ReleaseAndEndDuckIfNeeded(p);
                }
            }
        }

        /// <summary>재생 중이거나 로딩 대기 중인 모든 원샷을 정지합니다 (BGM은 그대로 유지).</summary>
        public void StopAllOneShots()
        {
            CancelAllPendingOneShotLoads();
            ReleaseAllActive();
        }

        /// <summary>현재 재생 중인 모든 것을 정지합니다: BGM과 활성 중인 모든 원샷.</summary>
        public void StopAll()
        {
            StopBgm();
            CancelAllPendingOneShotLoads();
            ReleaseAllActive();
        }

        private void CancelAllPendingOneShotLoads()
        {
            if (_pendingOneShotCounts.Count == 0)
            {
                return;
            }

            List<string> pendingIds = new List<string>(_pendingOneShotCounts.Keys);
            for (int i = 0; i < pendingIds.Count; i++)
            {
                string id = pendingIds[i];
                _oneShotStopTokens[id] = (_oneShotStopTokens.TryGetValue(id, out int t) ? t : 0) + 1;
            }
        }

        private void ReleaseAndEndDuckIfNeeded(SoundPlayer p)
        {
            bool wasDuckingVoice = _settings.DuckBgmOnVoice && p.CurrentChannel == ESoundChannel.Voice;
            _pool.Release(p);

            if (wasDuckingVoice)
            {
                EndDuck();
            }
        }

        private void ReleaseAllActive()
        {
            IReadOnlyList<SoundPlayer> active = _pool.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                SoundPlayer p = active[i];
                if (p != null)
                {
                    ReleaseAndEndDuckIfNeeded(p);
                }
            }
        }

        // ---- 볼륨 ----

        /// <summary>마스터 볼륨(0~1)을 설정하고 자동 저장합니다.</summary>
        public void SetMasterVolume(float volume)
        {
            _volumeSettings.master = Mathf.Clamp01(volume);
            ApplyVolumesToActivePlayers();
            SaveVolumeSettings();
        }

        /// <summary>채널별 볼륨(0~1)을 설정하고 자동 저장합니다.</summary>
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
                if (p == null || p.CurrentSound == null || !_soundData.TryGetValue(p.CurrentSound, out SoundEntry entry))
                {
                    continue;
                }

                p.Source.volume = ComputeVolume(p.CurrentChannel, entry.defaultVolume);
            }
        }

        private void SaveVolumeSettings()
        {
            if (SaveManager.Instance == null)
            {
                return;
            }

            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(SoundSettingsKey);
            SaveManager.Instance.Save(key, _volumeSettings);
            SaveManager.Instance.Flush();
        }

        private float ComputeVolume(ESoundChannel channel, float entryDefaultVolume)
        {
            return _volumeSettings.master * _volumeSettings.Get(channel) * entryDefaultVolume;
        }

        // ---- 덕킹 ----

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

        // ---- 헬퍼 ----

        private int CountActive(string id)
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

        private async Awaitable FadeVolume(AudioSource source, float from, float to, float duration, int myToken)
        {
            if (duration <= 0f)
            {
                if (myToken == _bgmPlayToken)
                {
                    source.volume = to;
                }

                return;
            }

            float time = 0f;
            source.volume = from;

            while (time < duration)
            {
                if (myToken != _bgmPlayToken)
                {
                    return;
                }

                time += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, to, time / duration);
                await Awaitable.NextFrameAsync();
            }

            if (myToken == _bgmPlayToken)
            {
                source.volume = to;
            }
        }

        private async Awaitable<AudioClip> LoadClip(string fileName)
        {
            if (_clipCache.TryGetValue(fileName, out AudioClip cached))
            {
                return cached;
            }

            if (_pendingClipLoads.TryGetValue(fileName, out Task<AudioClip> inFlight))
            {
                return await inFlight;
            }

            AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(fileName);
            Task<AudioClip> task = handle.Task;
            _pendingClipLoads[fileName] = task;

            AudioClip clip;
            try
            {
                clip = await task;
            }
            finally
            {
                _pendingClipLoads.Remove(fileName);
            }

            if (clip != null && !_isShuttingDown)
            {
                _clipCache[fileName] = clip;
                _clipHandles[fileName] = handle;
            }
            else
            {
                Addressables.Release(handle);
            }

            return clip;
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            _isShuttingDown = true;

            foreach (AsyncOperationHandle<AudioClip> handle in _clipHandles.Values)
            {
                Addressables.Release(handle);
            }

            _clipHandles.Clear();
            _clipCache.Clear();
        }
    }
}
