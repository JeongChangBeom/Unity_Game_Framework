using UnityEngine;

namespace GameFramework.Localization
{
    /// <summary>
    /// 프로젝트별 LocalizationManager 설정입니다. Assets/Create/Game Framework/
    /// Localization/Localization Manager Settings로 생성한 뒤 Assets/Resources/
    /// GameFramework/LocalizationManagerSettings.asset 경로에 두면, 씬 배치 없이도
    /// LocalizationManager가 찾을 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizationManagerSettings",
        menuName = "Game Framework/Localization/Localization Manager Settings")]
    public sealed class LocalizationManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/LocalizationManagerSettings";

        [Header("Table")]
        [Tooltip("Data Parsing이 생성한 Localization 테이블의 Resources 경로입니다. 시트 탭 이름을 다르게 지어서 생성된 클래스명이 다르면 여기를 맞춰주세요.")]
        public string TableResourcePath = "GeneratedTables/LocalizationTable";

        [Header("Language")]
        [Tooltip("저장된 언어도 없고 시스템 언어 매핑도 실패했을 때 사용할 기본 언어입니다.")]
        public ELanguage DefaultLanguage = ELanguage.None;

        [Tooltip("현재 언어에 번역이 비어있을 때 대신 사용할 언어입니다.")]
        public ELanguage FallbackLanguage = ELanguage.None;

        [Tooltip("저장된 언어 선택이 없는 첫 실행 시, Application.systemLanguage를 지원 언어로 매핑해서 사용할지 여부입니다. 매핑에 실패하면 Default Language를 사용합니다.")]
        public bool UseSystemLanguageOnFirstLaunch = true;
    }
}
