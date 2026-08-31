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
        [Tooltip("씬 오퍼레이션이 이 시간(초) 안에 활성화 준비 상태가 되지 않으면 실패로 처리합니다(재시도/폴백 대상). 0 이하이면 무한 대기합니다.")]
        [Min(0f)] public float SceneOperationTimeoutSeconds = 60f;

        [Tooltip("로딩 화면의 RequestShow/RequestHide 콜백이 이 시간(초) 안에 안 끝나면 성공으로 치고 넘어갑니다. 0 이하이면 무한 대기합니다.")]
        [Min(0f)] public float LoadingScreenTimeoutSeconds = 10f;

        [Tooltip("ISceneEntryPoint/ISceneExitPoint 훅 하나가 이 시간(초) 안에 안 끝나면 경고 로그를 남기고 건너뜁니다. 0 이하이면 무한 대기합니다.")]
        [Min(0f)] public float EntryExitPointTimeoutSeconds = 10f;

        [Tooltip("SceneLoadStep 하나가 이 시간(초) 안에 안 끝나면 실패 처리합니다. Critical=true(기본값)면 씬 로드 전체가 실패(재시도/폴백 대상)하고, Critical=false면 경고만 남기고 계속 진행됩니다. 0 이하이면 무한 대기합니다.")]
        [Min(0f)] public float LoadStepTimeoutSeconds = 30f;

        [Header("Retry & Fallback")]
        [Tooltip("로드 실패 시 자동 재시도 횟수. 0이면 재시도하지 않고 기존과 동일하게 즉시 실패 처리합니다.")]
        [Min(0)] public int MaxRetryCount = 0;

        [Tooltip("재시도 사이 대기 시간(초). 재시도 중에도 로딩 화면은 계속 떠 있습니다.")]
        [Min(0f)] public float RetryDelaySeconds = 1f;

        [Tooltip("모든 재시도가 실패했을 때 자동으로 이동할 폴백 씬 이름(Build Settings 기준). 비워두면 폴백 없이 실패로 끝냅니다.")]
        public string FallbackSceneName = "";

        [Tooltip("폴백 씬 로드 자체가 실패할 경우 폴백을 다시 시도할 횟수. 무한 루프 방지를 위해 기본값은 0(추가 재시도 없이 한 번만 시도)입니다. 이마저 실패하면 최종 실패로 끝납니다.")]
        [Min(0)] public int FallbackMaxRetryCount = 0;
    }
}
