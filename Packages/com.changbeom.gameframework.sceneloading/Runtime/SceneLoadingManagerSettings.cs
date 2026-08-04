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
        [Tooltip("씬 오퍼레이션 자신(Build Settings/Addressables 공통)이 활성화 준비 상태가 될 때까지 이 시간(초) 안에 끝나지 않으면 실패로 처리합니다(재시도/폴백 대상). Build Settings에서 비활성화된 씬 이름으로 로드를 시도하거나, Addressables 원격 다운로드가 멈추는 등 진행률이 전혀 안 올라가는 상태로 영원히 대기하는 것을 방지합니다. 같은 시간을, Build Settings 경로에서 실패한 로드를 정리(끝까지 로드시킨 뒤 언로드)할 때도 재사용합니다 - 정리 자체가 안 끝나면 재시도/폴백 파이프라인이 멈추기 때문입니다. 큰 씬/느린 다운로드를 고려해 넉넉하게 잡았습니다. 0 이하이면 타임아웃 없이 무한 대기합니다.")]
        [Min(0f)] public float SceneOperationTimeoutSeconds = 60f;

        [Tooltip("로딩 화면의 RequestShow/RequestHide 콜백(CompleteShow/CompleteHide)이 이 시간(초) 안에 호출되지 않으면 성공한 걸로 치고 넘어갑니다. LoadingScreenPrefabOverride로 만든 커스텀 로딩 화면이 CompleteShow()/CompleteHide()를 호출하지 않는 버그가 있어도 IsLoading이 영구 고착되지 않도록 방지합니다(내장 기본 로딩 화면은 이 문제가 없습니다). 0 이하이면 타임아웃 없이 무한 대기합니다.")]
        [Min(0f)] public float LoadingScreenTimeoutSeconds = 10f;

        [Tooltip("ISceneEntryPoint/ISceneExitPoint 훅 하나가 이 시간(초) 안에 끝나지 않으면 경고 로그를 남기고 건너뛴 뒤 다음 단계로 진행합니다. 사용자 코드의 버그로 훅이 영원히 끝나지 않아 씬 전환 전체가 멈추는 것을 방지합니다. 0 이하이면 타임아웃 없이 무한 대기합니다.")]
        [Min(0f)] public float EntryExitPointTimeoutSeconds = 10f;

        [Tooltip("LoadSceneAsync의 extraSteps로 전달한 SceneLoadStep 하나가 이 시간(초) 안에 끝나지 않으면 그 단계를 실패로 처리합니다 - EntryExitPointTimeoutSeconds와 달리 성공한 걸로 치고 넘어가지 않습니다. Critical(기본값)이면 씬 로드 자체도 실패로 처리되어 재시도/폴백 대상이 되고, Critical=false면 경고 로그만 남기고 씬 로드는 계속 진행됩니다. 네트워크 요청 등 오래 걸릴 수 있는 작업을 고려해 기본값을 EntryExitPointTimeoutSeconds보다 길게 잡았습니다. 0 이하이면 타임아웃 없이 무한 대기합니다.")]
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
