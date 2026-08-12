using UnityEngine;

namespace GameFramework.Graphics
{
    /// <summary>
    /// 프로젝트별 GraphicsManager 설정입니다. Assets/Create/Game Framework/Graphics/
    /// Graphics Manager Settings로 생성한 뒤 Assets/Resources/GameFramework/
    /// GraphicsManagerSettings.asset 경로에 두면, 씬 배치 없이도 GraphicsManager가
    /// 찾을 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GraphicsManagerSettings",
        menuName = "Game Framework/Graphics/Graphics Manager Settings")]
    public sealed class GraphicsManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/GraphicsManagerSettings";

        [Header("저장된 값이 없을 때(첫 실행) 사용할 기본값 - PC/모바일 공통")]
        [Tooltip("QualitySettings의 품질 단계 인덱스입니다.")]
        public int DefaultQualityLevel = 2;

        [Tooltip("프레임 상한(fps). 모바일에서 배터리/발열 관리에 특히 중요합니다.")]
        public int DefaultTargetFrameRate = 60;

        [Header("PC 전용 - 모바일에서는 효과 없음")]
        public bool DefaultFullscreen = true;
        public bool DefaultVSync = true;
    }
}
