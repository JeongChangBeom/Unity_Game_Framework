using UnityEngine;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 프로젝트별 SceneLoadingManager 설정입니다. Assets/Create/Game Framework/Scene
    /// Loading/Scene Loading Manager Settings로 생성한 뒤 Assets/Resources/GameFramework/
    /// SceneLoadingManagerSettings.asset 경로에 두면, 씬 배치 없이도 SceneLoadingManager가
    /// 찾을 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneLoadingManagerSettings",
        menuName = "Game Framework/Scene Loading/Scene Loading Manager Settings")]
    public sealed class SceneLoadingManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/SceneLoadingManagerSettings";

        [Header("Timing")]
        [Tooltip("로딩 화면이 최소 이 시간(초) 동안은 표시됩니다. 빠른 로드에서 화면이 깜빡이는 것을 방지합니다.")]
        [Min(0f)] public float MinimumLoadingScreenDuration = 0.5f;

        [Tooltip("로딩 화면 페이드 인/아웃 시간(초)")]
        [Min(0f)] public float FadeDuration = 0.25f;

        [Header("Visual")]
        [Tooltip("비워두면 내장 기본 로딩 화면(검은 배경 + 퍼센트 텍스트)을 사용합니다.")]
        public SceneLoadingScreenBase LoadingScreenPrefabOverride;

        [Header("Safety")]
        [Tooltip("ISceneEntryPoint/ISceneExitPoint 훅 하나가 이 시간(초) 안에 끝나지 않으면 경고 로그를 남기고 건너뛴 뒤 다음 단계로 진행합니다. 사용자 코드의 버그로 훅이 영원히 끝나지 않아 씬 전환 전체가 멈추는 것을 방지합니다. 0 이하이면 타임아웃 없이 무한 대기합니다.")]
        [Min(0f)] public float EntryExitPointTimeoutSeconds = 10f;
    }
}
