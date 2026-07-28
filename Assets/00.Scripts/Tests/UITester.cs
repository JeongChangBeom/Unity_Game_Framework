using GameFramework.UISystem;
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
            // Built at runtime (like PoolingTester's test cube) so this tester needs no
            // manually-authored toast prefab to demonstrate ShowToast/HideToast.
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
                Log("RequestPopup(B, High) -- preempts A if A is open");
            }

            if (GUILayout.Button("3) Request Popup A with Result Callback"))
            {
                UIManager.Instance.RequestPopup<bool>(_popupA, EPopupPriority.Normal,
                    result => Log($"Popup A result = {result} (true = closed via its own close button)"));
                Log("RequestPopup<bool>(A) -- close it with A's own close button to see the result");
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
                UIManager.Instance.ShowToast(_toastTemplate, "Item obtained!");
                Log("ShowToast(\"Item obtained!\")");
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

            Text text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
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
        private Text _text;

        public void SetText(Text text)
        {
            _text = text;
        }

        public override void Show(object payload)
        {
            base.Show(payload);

            if (_text != null)
            {
                _text.text = payload as string ?? "Toast!";
            }
        }
    }
}
