using UnityEngine;

namespace GameFramework.UISystem
{
    /// <summary>
    /// Project-specific UIManager configuration. Create via
    /// Assets/Create/Game Framework/UI/UI Manager Settings and place it at
    /// Assets/Resources/GameFramework/UIManagerSettings.asset so UIManager can find
    /// it with no scene placement required.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UIManagerSettings",
        menuName = "Game Framework/UI/UI Manager Settings")]
    public sealed class UIManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/UIManagerSettings";

        [Header("Canvas")]
        public Vector2 ReferenceResolution = new Vector2(1920, 1080);
        [Range(0f, 1f)] public float MatchWidthOrHeight = 0.5f;

        [Header("Modal Blocker")]
        public Color ModalBlockerColor = new Color(0f, 0f, 0f, 0.5f);

        [Header("Toast")]
        [Tooltip("ShowToast 호출 시 duration을 지정하지 않으면 사용되는 기본 노출 시간(초)")]
        public float DefaultToastDuration = 2f;

        [Header("Input")]
        [Tooltip("뒤로가기/Escape 키로 최상단 팝업을 닫을 수 있는지 여부 (팝업별로 CloseableByBackButton=false로 개별 예외 가능)")]
        public bool EnableBackButtonClose = true;
    }
}
