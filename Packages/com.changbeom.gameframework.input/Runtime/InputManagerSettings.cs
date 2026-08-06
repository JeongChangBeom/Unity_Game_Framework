using UnityEngine;

namespace GameFramework.InputSystem
{
    /// <summary>
    /// 프로젝트별 InputManager 설정입니다. Assets/Create/Game Framework/Input/Input
    /// Manager Settings로 생성한 뒤 Assets/Resources/GameFramework/InputManagerSettings.asset
    /// 경로에 두면, 씬 배치 없이도 InputManager가 찾을 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InputManagerSettings",
        menuName = "Game Framework/Input/Input Manager Settings")]
    public sealed class InputManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/InputManagerSettings";

        [Header("Rebind")]
        [Tooltip("인터랙티브 리바인딩 중 무시할 컨트롤 경로입니다 (마우스 이동/포인터 델타처럼 리바인딩 대상이 되면 안 되는 컨트롤).")]
        public string[] RebindExcludePaths = { "<Mouse>/position", "<Mouse>/delta" };

        [Tooltip("리바인딩 도중 이 경로를 누르면 리바인딩을 취소합니다.")]
        public string RebindCancelPath = "<Keyboard>/escape";
    }
}
