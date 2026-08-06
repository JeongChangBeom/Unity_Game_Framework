using GameFramework.InputSystem;
using GameFramework.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameFramework.Tests
{
    public sealed class InputTester : MonoBehaviour
    {
        private const int InteractBindingIndex = 0;

        private SimpleTestPopup _popupTemplate;

        private string _log = "";
        private Vector2 _scroll;

        private void Awake()
        {
            // 팝업 프리팹을 미리 준비하지 않아도 UIManager 연동(입력 차단/Cancel)을
            // 확인할 수 있도록 런타임에 직접 만듭니다.
            _popupTemplate = CreateTestPopup();
        }

        private void OnEnable()
        {
            InputManager.Instance.OnRebindStarted += HandleRebindStarted;
            InputManager.Instance.OnRebindCompleted += HandleRebindCompleted;
            InputManager.Instance.OnRebindCanceled += HandleRebindCanceled;
            InputManager.Instance.OnDeviceChange += HandleDeviceChange;
        }

        private void OnDisable()
        {
            if (InputManager.Instance == null)
            {
                return;
            }

            InputManager.Instance.OnRebindStarted -= HandleRebindStarted;
            InputManager.Instance.OnRebindCompleted -= HandleRebindCompleted;
            InputManager.Instance.OnRebindCanceled -= HandleRebindCanceled;
            InputManager.Instance.OnDeviceChange -= HandleDeviceChange;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("Input Tester");

            InputAction move = InputManager.Instance.Actions.Gameplay.Move;
            InputAction interact = InputManager.Instance.Actions.Gameplay.Interact;

            GUILayout.Label($"Move={move.ReadValue<Vector2>()}, Interact={interact.IsPressed()}");
            GUILayout.Label($"IsGameplayInputEnabled={InputManager.Instance.IsGameplayInputEnabled}");

            GUILayout.Space(10);
            GUILayout.Label("UIManager 연동");
            if (GUILayout.Button("1) 테스트 팝업 열기 (열려 있는 동안 Gameplay 입력 차단, Cancel(Esc)로 닫힘)"))
            {
                UIManager.Instance.RequestPopup(_popupTemplate, EPopupPriority.Normal);
                Log("RequestPopup(테스트 팝업)");
            }

            GUILayout.Space(10);
            GUILayout.Label($"리바인딩 - Interact 현재 바인딩: {interact.bindings[InteractBindingIndex].effectivePath}");
            if (GUILayout.Button("2) Interact 리바인딩 시작"))
            {
                InputManager.Instance.StartRebind(interact, InteractBindingIndex,
                    path => Log($"리바인딩 완료: {path}"));
                Log("StartRebind(Interact) -- 아무 키/버튼이나 입력하세요");
            }

            if (GUILayout.Button("3) 진행 중인 리바인딩 취소"))
            {
                InputManager.Instance.CancelActiveRebind();
                Log("CancelActiveRebind()");
            }

            if (GUILayout.Button("4) Interact 바인딩 초기화"))
            {
                InputManager.Instance.ResetBinding(interact, InteractBindingIndex);
                Log("ResetBinding(Interact)");
            }

            if (GUILayout.Button("5) 전체 바인딩 초기화"))
            {
                InputManager.Instance.ResetAllBindings();
                Log("ResetAllBindings()");
            }

            GUILayout.Space(10);
            GUILayout.Label("저장 / 복원 (Play 모드를 재시작해서 리바인딩이 유지되는지 확인)");
            if (GUILayout.Button("6) 저장"))
            {
                InputManager.Instance.SaveBindings();
                Log("SaveBindings()");
            }

            if (GUILayout.Button("7) 다시 로드"))
            {
                InputManager.Instance.LoadBindings();
                Log("LoadBindings()");
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

        private void HandleRebindStarted(InputAction action, int bindingIndex)
        {
            Log($"OnRebindStarted: {action.name}[{bindingIndex}]");
        }

        private void HandleRebindCompleted(InputAction action, int bindingIndex)
        {
            Log($"OnRebindCompleted: {action.name}[{bindingIndex}]");
        }

        private void HandleRebindCanceled(InputAction action, int bindingIndex)
        {
            Log($"OnRebindCanceled: {action.name}[{bindingIndex}]");
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            Log($"OnDeviceChange: {device.displayName} - {change}");
        }

        private SimpleTestPopup CreateTestPopup()
        {
            GameObject go = new GameObject("TestPopupTemplate");
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 300);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);

            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "테스트 팝업\n(Gameplay 입력이 막혀 있어야 합니다)\nCancel(Esc)로 닫힙니다";
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return go.AddComponent<SimpleTestPopup>();
        }
    }

    internal sealed class SimpleTestPopup : UIPopupBase
    {
    }
}
