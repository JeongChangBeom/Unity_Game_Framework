using UnityEngine;

namespace GameFramework.UISystem
{
    /// <summary>
    /// 프로젝트별 UIManager 설정입니다. Assets/Create/Game Framework/UI System/UI Manager
    /// Settings로 생성한 뒤 Assets/Resources/GameFramework/UIManagerSettings.asset
    /// 경로에 두면, 씬 배치 없이도 UIManager가 찾을 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UIManagerSettings",
        menuName = "Game Framework/UI System/UI Manager Settings")]
    public sealed class UIManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/UIManagerSettings";

        [Header("Canvas")]
        public Vector2 ReferenceResolution = new Vector2(1920, 1080);
        [Range(0f, 1f)] public float MatchWidthOrHeight = 0.5f;
        [Tooltip("UIManager가 사용하는 Canvas의 Sort Order입니다. UIManager는 부팅 시점에 씬에서 Canvas를 찾아 재사용하거나(못 찾으면) 새로 만드는데, 부팅 순서에 따라 어느 쪽이든 될 수 있어 이 값을 명시적으로 설정하지 않으면 프로젝트가 별도로 둔 다른 Canvas와 Sort Order가 동률이 되어 렌더링 순서가 보장되지 않습니다. HUD/팝업/토스트/오버레이가 항상 게임의 다른 모든 Canvas보다 위에 그려지도록 충분히 큰 값을 기본값으로 둡니다.")]
        public int CanvasSortOrder = 100;

        [Header("Modal Blocker")]
        public Color ModalBlockerColor = new Color(0f, 0f, 0f, 0.5f);

        [Header("Toast")]
        [Tooltip("ShowToast 호출 시 duration을 지정하지 않으면 사용되는 기본 노출 시간(초)")]
        public float DefaultToastDuration = 2f;

        [Header("Input")]
        [Tooltip("뒤로가기/Escape 키로 최상단 팝업을 닫을 수 있는지 여부 (팝업별로 CloseableByBackButton=false로 개별 예외 가능)")]
        public bool EnableBackButtonClose = true;

        [Header("Persistent Layer Prefabs")]
        [Tooltip("HudRoot 밑에 초기화 시점에 미리 생성해둘 프리팹. 시작 시 비활성화 상태로 생성되며, 필요할 때 UIManager.HudInstance를 SetActive(true)하세요. 비워두면 아무 것도 생성하지 않습니다.")]
        public GameObject HudPrefabOverride;

        [Tooltip("OverlayRoot 밑에 초기화 시점에 미리 생성해둘 프리팹. 시작 시 비활성화 상태로 생성되며, 필요할 때 UIManager.OverlayInstance를 SetActive(true)하세요. 비워두면 아무 것도 생성하지 않습니다.")]
        public GameObject OverlayPrefabOverride;
    }
}
