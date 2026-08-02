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
        // Data Parsing이 생성하는 Sound 테이블(SoundTable)은 프로젝트 쪽 어셈블리에 있어서
        // 이 패키지가 타입으로 직접 참조할 수 없습니다 (패키지는 프로젝트를 참조할 수 없음).
        // 그래서 부팅 시 1회, ScriptableObject로만 로드해 리플렉션으로 필드를 읽고
        // ESound 기준 Dictionary로 캐싱해둡니다. 이후 조회는 전부 이 캐시로만 처리되므로
        // 런타임 리플렉션 비용은 부팅 시 1회로 끝납니다.
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
        private Dictionary<ESound, SoundEntry> _soundData;
        private SoundSettingsData _volumeSettings;
        private SoundPlayerPool _pool;

        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _clipHandles = new Dictionary<string, AsyncOperationHandle<AudioClip>>();
        private readonly Dictionary<string, Task<AudioClip>> _pendingClipLoads = new Dictionary<string, Task<AudioClip>>();
        private readonly Dictionary<ESound, int> _pendingOneShotCounts = new Dictionary<ESound, int>();

        private AudioSource _bgmSource;
        private ESound _currentBgm = ESound.None;
        private ESound _requestedBgm = ESound.None;
        private int _bgmPlayToken;

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

        private static Dictionary<ESound, SoundEntry> LoadSoundData(string resourcePath)
        {
            Dictionary<ESound, SoundEntry> data = new Dictionary<ESound, SoundEntry>();

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

                if (!Enum.TryParse(fileName, out ESound id) || !Enum.IsDefined(typeof(ESound), id))
                {
                    Debug.LogWarning($"[SoundManager] FileName \"{fileName}\"에 해당하는 ESound 멤버가 없습니다. Game Framework/Sound System/Generate ESound + Register Addressables로 다시 생성해보세요.");
                    continue;
                }

                string channelRaw = GetFieldValue<string>(rowType, row, "channel");

                data[id] = new SoundEntry
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

            foreach (ESound id in _settings.PreloadSounds)
            {
                if (id == ESound.None)
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

            if (_currentBgm != ESound.None && _soundData.TryGetValue(_currentBgm, out SoundEntry bgmEntry))
            {
                _bgmSource.volume = ComputeVolume(ESoundChannel.BGM, bgmEntry.defaultVolume) * _duckCurrentMultiplier;
            }
        }

        private void OnOneShotReturned(ESound finishedId)
        {
            // 원샷 사운드가 실제로 끝났다는 것을 확실히 아는 유일한 지점입니다
            // (Tick()이 풀의 IsFinished() 폴링을 담당합니다) -- PlayOneShot 안에 별도의
            // 폴링 루프를 두는 대신 여기서 덕킹을 종료하면, 두 번째 폴러가 원래 사운드가
            // 끝난 걸 알아채기 전에 풀이 SoundPlayer를 새 사운드용으로 재활용해버리는
            // 경쟁 상태를 피할 수 있습니다.
            if (_settings.DuckBgmOnVoice &&
                _soundData.TryGetValue(finishedId, out SoundEntry entry) &&
                entry.channel == ESoundChannel.Voice)
            {
                EndDuck();
            }
        }

        // ---- 재생 ----

        public void PlaySound(ESound id)
        {
            if (id == ESound.None)
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

        private async Awaitable PlayBgm(ESound id, SoundEntry entry)
        {
            if (_requestedBgm == id)
            {
                return;
            }

            int myToken = ++_bgmPlayToken;
            _requestedBgm = id;

            AudioClip clip = await LoadClip(entry.fileName);

            if (myToken != _bgmPlayToken)
            {
                // 그 사이 다른 PlayBgm/StopBgm 호출이 있었다는 뜻입니다. _requestedBgm은
                // 이미 그 호출이 관리하고 있으므로 여기서 건드리면 안 됩니다.
                return;
            }

            if (clip == null)
            {
                // 로드 실패입니다. _requestedBgm을 이 id로 남겨두면, 이후 같은 id로
                // PlaySound를 다시 호출해도 위쪽의 "if (_requestedBgm == id) return;"에
                // 막혀 영원히 재생되지 않습니다. 실제로 지금 재생 중인 곡(없으면 None)으로 되돌립니다.
                _requestedBgm = _currentBgm;
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
            _requestedBgm = ESound.None;
            _bgmSource.Stop();
            _bgmSource.clip = null;
            _currentBgm = ESound.None;
        }

        private async Awaitable PlayOneShot(ESound id, SoundEntry entry)
        {
            int maxConcurrent = Mathf.Max(1, entry.maxConcurrent);
            int pending = _pendingOneShotCounts.TryGetValue(id, out int p) ? p : 0;

            if (CountActive(id) + pending >= maxConcurrent)
            {
                return;
            }

            // _pool.Active는 Rent()가 성공한 뒤에야 늘어나므로, 아직 캐싱 안 된 사운드의
            // 로드를 기다리는 동안(clip == null || null 반환) CountActive만 보면 그 사이의
            // 중복 요청이 전부 같은(낮은) 값을 보고 maxConcurrent 제한을 그냥 통과해버립니다.
            // await 전에 자리를 미리 예약해서 이 창을 막습니다.
            _pendingOneShotCounts[id] = pending + 1;

            try
            {
                AudioClip clip = await LoadClip(entry.fileName);
                if (clip == null)
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
        public void StopSound(ESound id)
        {
            if (id == ESound.None)
            {
                return;
            }

            // _currentBgm이 아니라 _requestedBgm으로 확인합니다. _currentBgm은 로드와
            // 크로스페이드가 끝난 뒤에야 채워지므로, 아직 로딩 중인 BGM은 _currentBgm과
            // 절대 같아지지 않아 이 시점에 StopSound를 호출해도 무시되고 로드가 끝나면
            // 그대로 재생돼버렸습니다.
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
                    _pool.Release(p);
                }
            }
        }

        public void StopAllOneShots()
        {
            _pool.StopAll();
        }

        /// <summary>현재 재생 중인 모든 것을 정지합니다: BGM과 활성 중인 모든 원샷.</summary>
        public void StopAll()
        {
            StopBgm();
            StopAllOneShots();
        }

        // ---- 볼륨 ----

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
                if (p == null || !_soundData.TryGetValue(p.CurrentSound, out SoundEntry entry))
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

            // 같은 fileName에 대한 로드가 이미 진행 중이면(예: PreloadSounds가 아직 로딩
            // 중인데 게임플레이 쪽에서 같은 사운드를 재생 요청) 새로 Addressables 로드를
            // 또 시작하지 않고 진행 중인 것을 같이 기다립니다. 안 그러면 handle이 여러 개
            // 생겨서 _clipHandles[fileName]에 마지막 것만 남고 나머지는 Release되지 않은
            // 채 참조 카운트가 영구히 새는 것도 문제고, 로드 자체도 중복으로 낭비됩니다.
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

            if (clip != null)
            {
                // handle을 들고 있지 않으면 Addressables 참조 카운트를 절대 낮출 수 없어서,
                // 로드한 클립마다 세션 내내 번들이 고정(pin)된 채로 남습니다. 클립 캐시
                // 자체는 앱 생명주기 동안 유지하는 게 의도된 설계지만, 참조 카운트는
                // 정확히 짝을 맞춰둬야 나중에 세션 중 캐시를 비우는 기능을 추가하거나
                // OnApplicationQuit에서 정리할 때 이 handle을 그대로 쓸 수 있습니다.
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

            foreach (AsyncOperationHandle<AudioClip> handle in _clipHandles.Values)
            {
                Addressables.Release(handle);
            }

            _clipHandles.Clear();
            _clipCache.Clear();
        }
    }
}
