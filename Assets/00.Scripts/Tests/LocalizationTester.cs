using System;
using GameFramework.Localization;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class LocalizationTester : MonoBehaviour
    {
        private string _log = "";
        private Vector2 _scroll;

        private void OnEnable()
        {
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            if (LocalizationManager.Instance == null)
            {
                return;
            }

            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("Localization Tester");

            GUILayout.Label($"CurrentLanguage={LocalizationManager.Instance.CurrentLanguage}");

            GUILayout.Space(10);
            GUILayout.Label("키별 텍스트 (None 제외 - 실제 등록된 키만 매 프레임 조회해도 안전함)");

            Array keys = Enum.GetValues(typeof(ELocKey));
            foreach (ELocKey key in keys)
            {
                if (key == ELocKey.None)
                {
                    continue;
                }

                GUILayout.Label($"{key} = {LocalizationManager.Instance.GetText(key)}");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("존재하지 않는 키 조회 테스트 (None - 에러 로그 1회만 남아야 함)"))
            {
                Log($"GetText(None) = {LocalizationManager.Instance.GetText(ELocKey.None)}");
            }

            GUILayout.Space(10);
            GUILayout.Label("언어 전환 (Data Parsing으로 실제 Localization 시트를 만들면 실제 언어 버튼이 늘어납니다)");

            Array languages = Enum.GetValues(typeof(ELanguage));
            foreach (ELanguage language in languages)
            {
                if (GUILayout.Button($"{language}로 전환"))
                {
                    _ = ChangeLanguage(language);
                }
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Clear Log"))
            {
                _log = "";
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
            GUILayout.TextArea(_log);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private async Awaitable ChangeLanguage(ELanguage language)
        {
            Log($"SetLanguageAsync({language}) 시작");
            await LocalizationManager.Instance.SetLanguageAsync(language);
            Log($"SetLanguageAsync({language}) 완료");
        }

        private void HandleLanguageChanged(string language)
        {
            Log($"OnLanguageChanged: {language}");
        }

        private void Log(string msg)
        {
            string line = System.DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
