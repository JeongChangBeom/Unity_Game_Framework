using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Localization
{
    /// <summary>KeyName을 지정해두면 언어가 바뀔 때마다 자동으로 텍스트를 갱신합니다.
    /// 레거시 `Text`와 `TextMeshProUGUI`를 둘 다 지원합니다 - GameObject에 둘 중 하나만
    /// 붙어 있으면 자동으로 그쪽에 반영됩니다. 둘 다 없으면 에러 로그를 남깁니다.
    /// Key는 `ELocKey`가 이 프로젝트 쪽에 생성되는 타입이라 패키지가 제공하는 컴포넌트에서
    /// 강타입으로 참조할 수 없어 문자열로 직접 입력합니다 (Localization 시트의
    /// KeyName 컬럼 값과 정확히 같아야 합니다).</summary>
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;

        private Text _text;
        private TextMeshProUGUI _tmpText;

        private void Awake()
        {
            _text = GetComponent<Text>();
            _tmpText = GetComponent<TextMeshProUGUI>();

            if (_text == null && _tmpText == null)
            {
                Debug.LogError($"[LocalizedText] \"{name}\"에 Text나 TextMeshProUGUI 컴포넌트가 없습니다. 둘 중 하나를 붙여야 텍스트가 표시됩니다.", this);
            }
        }

        private void OnEnable()
        {
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
            string text = LocalizationManager.Instance.GetText(_key);

            if (_text != null)
            {
                _text.text = text;
            }

            if (_tmpText != null)
            {
                _tmpText.text = text;
            }
        }
    }
}
