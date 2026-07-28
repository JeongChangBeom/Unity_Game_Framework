using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework.Sound
{
    /// <summary>
    /// Project-specific SoundManager configuration. Create via
    /// Assets/Create/Game Framework/Sound/Sound Manager Settings and place it at
    /// Assets/Resources/GameFramework/SoundManagerSettings.asset so SoundManager can find
    /// it with no scene placement required.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SoundManagerSettings",
        menuName = "Game Framework/Sound/Sound Manager Settings")]
    public sealed class SoundManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/SoundManagerSettings";

        [Header("Pool")]
        public int InitialPoolSize = 10;
        public int MaxPoolSize = 30;

        [Header("Mixer Routing (optional -- leave empty to skip mixer routing)")]
        public AudioMixerGroup BgmMixerGroup;
        public AudioMixerGroup SfxMixerGroup;
        public AudioMixerGroup UiMixerGroup;
        public AudioMixerGroup VoiceMixerGroup;

        [Header("BGM")]
        [Tooltip("BGM 교체 시 크로스페이드(페이드아웃+페이드인) 길이(초)")]
        public float BgmCrossfadeSeconds = 0.5f;

        [Header("Ducking")]
        [Tooltip("Voice 채널 사운드 재생 중 BGM 볼륨을 자동으로 낮출지 여부")]
        public bool DuckBgmOnVoice = true;
        [Range(0f, 1f)] public float DuckedBgmVolumeScale = 0.3f;
        public float DuckFadeSeconds = 0.2f;

        [Header("Preload")]
        [Tooltip("씬 시작 시 미리 로드해둘 사운드 (첫 재생 지연 방지)")]
        public ESound[] PreloadSounds;
    }
}
