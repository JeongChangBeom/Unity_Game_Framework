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

        // StopSound(id)가 아직 클립 로딩이 끝나지 않아 _pool.Active에 잡히지 않는 원샷
        // 요청까지 취소할 수 있도록 하는 세대 토큰입니다. PlayOneShot이 시작 시점의 값을
        // 들고 있다가, await 이후 값이 바뀌어 있으면(그 사이 StopSound가 호출됨) 재생을
        // 시작하지 않고 조용히 취소합니다.
        private readonly Dictionary<ESound, int> _oneShotStopTokens = new Dictionary<ESound, int>();

        // OnApplicationQuit 이후에 완료되는 LoadClip이 이미 비워진 _clipCache/_clipHandles에
        // 다시 항목을 채워 넣으면 그 handle을 아무도 Release하지 못해 영구히 새므로,
        // 종료 이후에는 캐싱하지 않고 handle을 즉시 Release합니다.
        private bool _isShuttingDown;

        private AudioSource _bgmSource;
        private ESound _currentBgm = ESound.None;
        private ESound _requestedBgm = ESound.None;
        private int _bgmPlayToken;

        // PlayBgm의 크로스페이드가 _bgmSource.volume을 직접 애니메이션하는 동안,
        // Update()가 같은 프레임에 덕킹 기준으로 볼륨을 되돌려버리면 페이드아웃이
        // 멈추거나 페이드인이 목표값으로 즉시 튀는 문제가 있었습니다. 크로스페이드가
        // 진행 중인 동안은 Update()가 볼륨을 건드리지 않도록 이 플래그로 막습니다.
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

            if (!_bgmCrossfading && _currentBgm != ESound.None && _soundData.TryGetValue(_currentBgm, out SoundEntry bgmEntry))
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

            // 이 호출 자신이 실제로 _bgmCrossfading을 켰는지 기록합니다. clip 로드가
            // 실패하면 이 호출은 _bgmCrossfading을 한 번도 켜지 않은 채 finally에
            // 도달하는데, 그때 "내가 여전히 최신 토큰이다"라는 이유만으로 플래그를
            // 꺼버리면 실제로는 아직 페이드 중인 이전(선점당한) 호출의 진행 상태를
            // 대신 꺼버리는 문제가 있었습니다.
            bool crossfadeStartedByMe = false;

            try
            {
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

                // 크로스페이드가 끝날 때까지 Update()가 볼륨을 건드리지 않도록 막습니다.
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
                // 이 호출이 실제로 크로스페이드를 켠 당사자이면서, 여전히 "최신" 요청일
                // 때만 플래그를 풉니다. 더 최근에 시작된 PlayBgm 호출이 있다면(myToken이
                // 이미 바뀜), 그 호출이 아직 자기 크로스페이드를 진행 중일 수 있으므로
                // 여기서 먼저 풀어버리면 그 크로스페이드가 다시 Update()와 충돌하게 됩니다.
                if (crossfadeStartedByMe && myToken == _bgmPlayToken)
                {
                    _bgmCrossfading = false;
                }
            }
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
            int myStopToken = _oneShotStopTokens.TryGetValue(id, out int st) ? st : 0;

            try
            {
                AudioClip clip = await LoadClip(entry.fileName);
                if (clip == null)
                {
                    return;
                }

                // 로딩을 기다리는 동안 StopSound(id)가 호출됐으면(토큰이 바뀜) 재생을
                // 시작하지 않고 취소합니다 - "현재 재생 중인 모든 인스턴스를 정지"라는
                // StopSound의 의도상, 아직 로딩 중이던 요청도 취소 대상에 포함됩니다.
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
        public void StopSound(ESound id)
        {
            if (id == ESound.None)
            {
                return;
            }

            // 아직 클립 로딩 중이라 _pool.Active에 안 잡히는 대기 중인 원샷 요청도
            // 취소되도록 세대 토큰을 올립니다 (PlayOneShot 참고).
            _oneShotStopTokens[id] = (_oneShotStopTokens.TryGetValue(id, out int t) ? t : 0) + 1;

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
                    ReleaseAndEndDuckIfNeeded(p);
                }
            }
        }

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

        // ReleaseAllActive는 _pool.Active만 훑기 때문에, 아직 클립 로딩 중이라 거기 안
        // 잡히는 원샷 요청은 StopAllOneShots/StopAll을 불러도 그대로 살아남아 로드가
        // 끝나는 대로 재생돼버렸습니다. StopSound(id)가 쓰는 것과 동일한 세대 토큰
        // 메커니즘을, 지금 로딩 중인 모든 id에 대해 한 번에 적용합니다.
        private void CancelAllPendingOneShotLoads()
        {
            if (_pendingOneShotCounts.Count == 0)
            {
                return;
            }

            List<ESound> pendingIds = new List<ESound>(_pendingOneShotCounts.Keys);
            for (int i = 0; i < pendingIds.Count; i++)
            {
                ESound id = pendingIds[i];
                _oneShotStopTokens[id] = (_oneShotStopTokens.TryGetValue(id, out int t) ? t : 0) + 1;
            }
        }

        // _pool.Release/_pool.StopAll을 직접 부르면 자연 종료 시(Tick -> OnOneShotReturned)와
        // 달리 덕킹 종료 훅이 안 불려서, Voice 사운드를 자연 종료 전에 정지시키면
        // _voiceActiveCount가 영원히 안 줄어들어 BGM이 계속 덕킹된 채로 남을 수 있었습니다.
        // 여기서 직접 정지시키는 경로 전부 이 헬퍼를 거치도록 통일합니다.
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
            // 앱 종료 중에는 매니저 종료 순서가 보장되지 않아 SaveManager.Instance가
            // 이미 null일 수 있습니다.
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

        // myToken은 이 페이드를 시작한 PlayBgm 호출의 토큰입니다. 매 프레임 _bgmPlayToken과
        // 비교해서, 그 사이 더 최신 PlayBgm 호출이 시작됐으면(토큰이 바뀌었으면) 즉시
        // 멈춥니다 - 안 그러면 아직 진행 중인 이전 페이드와 방금 시작된 새 페이드가 같은
        // AudioSource.volume을 매 프레임 동시에 덮어쓰는 경쟁이 생깁니다. await 하나가
        // 끝난 뒤에만 토큰을 확인하는 것으로는 부족합니다 - 그 await 자체가 여러 프레임에
        // 걸쳐 진행되는 동안 다른 호출이 끼어들 수 있기 때문에, 루프 안에서 매 프레임 확인합니다.
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

            if (clip != null && !_isShuttingDown)
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
                // OnApplicationQuit이 이미 지나간 뒤에 완료된 로드라면(clip이 있어도)
                // 캐시에 다시 채워 넣지 않고 바로 Release합니다 - 안 그러면 아무도 이
                // handle을 정리해줄 기회가 없어 참조 카운트가 영구히 샙니다.
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
