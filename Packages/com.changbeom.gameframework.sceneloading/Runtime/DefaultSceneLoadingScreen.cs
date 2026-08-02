using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 별도 프리팹을 지정하지 않았을 때 쓰이는 기본 로딩 화면입니다. 검은 배경과
    /// 진행률 텍스트만 있는 최소 구성이며, 코드로 절차적으로 생성됩니다.
    /// UI 패키지 Runtime이 TextMeshPro에 의존하지 않는 것과 일관되게 레거시 Text를
    /// 사용합니다. 다른 비주얼이 필요하면 SceneLoadingManagerSettings의
    /// LoadingScreenPrefabOverride로 원하는 프리팹(TMP 포함)으로 교체하세요.
    /// </summary>
    internal sealed class DefaultSceneLoadingScreen : SceneLoadingScreenBase
    {
        private Text _percentText;

        internal static DefaultSceneLoadingScreen Create(Transform parent)
        {
            GameObject root = new GameObject("[DefaultSceneLoadingScreen]");
            root.transform.SetParent(parent, false);

            RectTransform rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;

            Image background = root.AddComponent<Image>();
            background.color = Color.black;

            GameObject textGo = new GameObject("PercentText");
            textGo.transform.SetParent(root.transform, false);

            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.sizeDelta = new Vector2(320f, 60f);

            Text text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "Loading... 0%";

            DefaultSceneLoadingScreen screen = root.AddComponent<DefaultSceneLoadingScreen>();
            screen._percentText = text;

            root.SetActive(false);
            return screen;
        }

        public override void SetProgress(float progress01)
        {
            if (_percentText != null)
            {
                _percentText.text = $"Loading... {Mathf.RoundToInt(progress01 * 100f)}%";
            }
        }
    }
}
