using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Localization
{
    // UI 패키지 Runtime이 TextMeshPro에 의존하지 않는 것과 일관되게 레거시 Text를
    // 사용합니다 (Scene Loading의 DefaultSceneLoadingScreen과 동일한 이유).
    /// <summary>KeyName을 지정해두면 언어가 바뀔 때마다 자동으로 텍스트를 갱신합니다.
    /// ELocKey는 이 프로젝트 쪽에 생성되는 타입이라 패키지가 제공하는 컴포넌트에서
    /// 강타입으로 참조할 수 없어 문자열로 직접 입력합니다 (Localization 시트의
    /// KeyName 컬럼 값과 정확히 같아야 합니다).</summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;

        private Text _text;

        private void Awake()
        {
            _text = GetComponent<Text>();
        }

        private void OnEnable()
        {
            // 앱 종료 중에는 매니저 종료 순서가 보장되지 않아 LocalizationManager.Instance가
            // 이미 null일 수 있습니다 (아래 OnDisable과 동일한 이유의 방어).
            if (LocalizationManager.Instance == null)
            {
                return;
            }

            LocalizationManager.Instance.OnLanguageChanged += Refresh;
            Refresh(LocalizationManager.Instance.CurrentLanguage);
        }

        private void OnDisable()
        {
            if (LocalizationManager.Instance == null)
            {
                return;
            }

            LocalizationManager.Instance.OnLanguageChanged -= Refresh;
        }

        private void Refresh(string _)
        {
            _text.text = LocalizationManager.Instance.GetText(_key);
        }
    }
}
