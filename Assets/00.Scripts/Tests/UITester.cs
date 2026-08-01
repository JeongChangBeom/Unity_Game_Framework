using GameFramework.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Tests
{
    public sealed class UITester : MonoBehaviour
    {
        [SerializeField] private UIPopupBase _popupA;
        [SerializeField] private UIPopupBase _popupB;

        private SimpleTestToast _toastTemplate;

        private string _log = "";
        private Vector2 _scroll;

        private void Awake()
        {
            // 런타임에 직접 만들어서, 토스트 프리팹을 미리 준비하지 않아도
            // ShowToast/HideToast 동작을 확인할 수 있습니다.
            _toastTemplate = CreateToastTemplate();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("UI Tester");

            GUILayout.Label($"IsAnyPopupOpen={UIManager.Instance.IsAnyPopupOpen}, HasPendingPopups={UIManager.Instance.HasPendingPopups}");

            GUILayout.Space(10);
            GUILayout.Label("Popup");
            if (GUILayout.Button("1) Request Popup A (Normal)"))
            {
                UIManager.Instance.RequestPopup(_popupA, EPopupPriority.Normal);
                Log("RequestPopup(A, Normal)");
            }

            if (GUILayout.Button("2) Request Popup B (High, PreemptIfHigher)"))
            {
                UIManager.Instance.RequestPopup(_popupB, EPopupPriority.High);
                Log("RequestPopup(B, High) -- A가 열려 있으면 선점합니다");
            }

            if (GUILayout.Button("3) Request Popup A with Result Callback"))
            {
                UIManager.Instance.RequestPopup<bool>(_popupA, EPopupPriority.Normal,
                    result => Log($"Popup A 결과 = {result} (true = 팝업 자체 닫기 버튼으로 닫힘)"));
                Log("RequestPopup<bool>(A) -- 결과를 보려면 A 자체의 닫기 버튼으로 닫으세요");
            }

            if (GUILayout.Button("4) Close Top Popup"))
            {
                UIManager.Instance.CloseTopPopup();
                Log("CloseTopPopup()");
            }

            if (GUILayout.Button("5) Close All + Clear Queue"))
            {
                UIManager.Instance.CloseAll();
                Log("CloseAll()");
            }

            GUILayout.Space(10);
            GUILayout.Label("Toast (non-modal, auto-dismiss)");
            if (GUILayout.Button("6) Show Toast (2s)"))
            {
                UIManager.Instance.ShowToast(_toastTemplate, "아이템 획득!");
                Log("ShowToast(\"아이템 획득!\")");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Clear Log"))
            {
                _log = "";
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
            GUILayout.TextArea(_log);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private SimpleTestToast CreateToastTemplate()
        {
            GameObject go = new GameObject("TestToastTemplate");
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 80);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.8f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);

            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            SimpleTestToast toast = go.AddComponent<SimpleTestToast>();
            toast.SetText(text);

            return toast;
        }

        private void Log(string msg)
        {
            string line = System.DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }

    internal sealed class SimpleTestToast : UIToastBase
    {
        private TextMeshProUGUI _text;

        public void SetText(TextMeshProUGUI text)
        {
            _text = text;
        }

        public override void Show(object payload)
        {
            base.Show(payload);

            if (_text != null)
            {
                _text.text = payload as string ?? "토스트!";
            }
        }
    }
}
