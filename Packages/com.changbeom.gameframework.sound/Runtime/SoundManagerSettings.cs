using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework.SoundSystem
{
    /// <summary>
    /// 프로젝트별 SoundManager 설정입니다. Assets/Create/Game Framework/Sound System/Sound
    /// Manager Settings로 생성한 뒤 Assets/Resources/GameFramework/SoundManagerSettings.asset
    /// 경로에 두면, 씬 배치 없이도 SoundManager가 찾을 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SoundManagerSettings",
        menuName = "Game Framework/Sound System/Sound Manager Settings")]
    public sealed class SoundManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/SoundManagerSettings";

        [Header("Sound Table")]
        [Tooltip("Data Parsing으로 생성된 Sound 테이블의 Resources 경로입니다 (기본: GeneratedTables/SoundTable). 시트 탭 이름을 다르게 지어서 클래스 이름이 달라졌다면 여기를 맞춰주세요.")]
        public string SoundTableResourcePath = "GeneratedTables/SoundTable";

        [Header("Pool")]
        public int InitialPoolSize = 10;
        public int MaxPoolSize = 30;

        [Header("Mixer Routing (선택 -- 비워두면 믹서 라우팅을 사용하지 않음)")]
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
        [Tooltip("씬 시작 시 미리 로드해둘 사운드의 FileName (첫 재생 지연 방지). ESound는 이 " +
            "프로젝트 쪽에 생성되는 타입이라 패키지 설정에서 강타입으로 참조할 수 없어 문자열로 " +
            "직접 입력합니다 (Data Parsing Sound 시트의 FileName 컬럼 값과 정확히 같아야 합니다).")]
        public string[] PreloadSounds;
    }
}
