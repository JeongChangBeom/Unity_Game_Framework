using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Localization
{
    // UI 패키지 Runtime이 TextMeshPro에 의존하지 않는 것과 일관되게 레거시 Text를
    // 사용합니다 (Scene Loading의 DefaultSceneLoadingScreen과 동일한 이유).
    /// <summary>ELocKey를 지정해두면 언어가 바뀔 때마다 자동으로 텍스트를 갱신합니다.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private ELocKey _key;

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

        private void Refresh(ELanguage _)
        {
            _text.text = LocalizationManager.Instance.GetText(_key);
        }
    }
}
