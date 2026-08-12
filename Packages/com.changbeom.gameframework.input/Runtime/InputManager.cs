using System;
using GameFramework.Core;
using GameFramework.InputSystem.Generated;
using GameFramework.SaveLoad;
using GameFramework.UISystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameFramework.InputSystem
{
    /// <summary>
    /// Unity Input System(GameFrameworkInputActions)을 감싸는 매니저입니다. UIManager가
    /// 모달 팝업을 띄우고 있으면 Gameplay 액션맵을 자동으로 비활성화하고(UI 액션맵은 항상
    /// 켜둠), 인터랙티브 리바인딩과 SaveManager를 통한 리바인딩 결과 저장/복원을 제공합니다.
    /// 로컬 멀티플레이(여러 기기-플레이어 페어링)는 지원하지 않습니다.
    ///
    /// 이 패키지의 네임스페이스(GameFramework.InputSystem)가 Unity의 UnityEngine.InputSystem과
    /// 이름이 겹칩니다. 특히 Unity 쪽에는 자기 네임스페이스와 이름이 같은 정적 클래스
    /// InputSystem(디바이스 이벤트 등)이 있어서, 이 파일 안에서는 혼동을 피하려고
    /// UnityEngine.InputSystem.InputSystem처럼 항상 완전한 이름으로 씁니다.
    /// </summary>
    public sealed class InputManager : MonoSingleton<InputManager>
    {
        private const string SettingsDomain = "settings";
        private const string InputSettingsKey = "input";

        private InputManagerSettings _settings;
        private GameFrameworkInputActions _actions;
        private InputActionRebindingExtensions.RebindingOperation _activeRebind;
        private bool _lastGameplayBlocked;

        public GameFrameworkInputActions Actions => _actions;
        public bool IsGameplayInputEnabled => _actions.Gameplay.enabled;

        public event Action<InputAction, int> OnRebindStarted;
        public event Action<InputAction, int> OnRebindCompleted;
        public event Action<InputAction, int> OnRebindCanceled;
        public event Action<InputDevice, InputDeviceChange> OnDeviceChange;

        protected override void OnInitialize()
        {
            _settings = LoadSettings();
            _actions = new GameFrameworkInputActions();

            _actions.Gameplay.Enable();
            _actions.UI.Enable();
            _actions.UI.Cancel.performed += HandleCancelPerformed;

            UnityEngine.InputSystem.InputSystem.onDeviceChange += HandleDeviceChange;

            LoadBindings();
        }

        private static InputManagerSettings LoadSettings()
        {
            InputManagerSettings settings = Resources.Load<InputManagerSettings>(InputManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[InputManager] Resources/{InputManagerSettings.ResourcePath}에서 InputManagerSettings 에셋을 찾지 못했습니다. 기본값을 사용합니다. Assets/Create/Game Framework/Input/Input Manager Settings로 에셋을 만드세요.");
            return ScriptableObject.CreateInstance<InputManagerSettings>();
        }

        // UIManager에는 팝업 열림/닫힘 이벤트가 없어(IsBlockingInput은 폴링 전용 bool),
        // 매 프레임 상태가 바뀌었을 때만 Gameplay 맵을 켜고 끕니다(매 프레임 무조건
        // Enable/Disable을 부르면 낭비이므로 edge에서만 호출).
        private void Update()
        {
            if (UIManager.Instance == null)
            {
                return;
            }

            bool blocked = UIManager.Instance.IsBlockingInput;

            if (blocked == _lastGameplayBlocked)
            {
                return;
            }

            _lastGameplayBlocked = blocked;

            if (blocked)
            {
                _actions.Gameplay.Disable();
            }
            else
            {
                _actions.Gameplay.Enable();
            }
        }

        // PC의 Esc 키와 Android 뒤로가기 버튼은 Unity 내부적으로 같은 Escape 키 이벤트로
        // 들어오므로, UI/Cancel 액션 하나로 두 플랫폼을 함께 다룰 수 있습니다. 팝업별
        // CloseableByBackButton opt-out은 UIManager가 그대로 소유하고, 여기서는 그 값을
        // 읽기만 합니다.
        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            if (UIManager.Instance == null || !UIManager.Instance.CurrentPopupCloseableByBackButton)
            {
                return;
            }

            UIManager.Instance.CloseTopPopup();
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            SafeInvoke(OnDeviceChange, device, change, nameof(OnDeviceChange));
        }

        /// <summary>지정한 액션의 바인딩 하나를 인터랙티브하게 리바인딩합니다. 이미 진행 중인
        /// 리바인딩이 있으면 먼저 취소합니다.</summary>
        public InputActionRebindingExtensions.RebindingOperation StartRebind(InputAction action, int bindingIndex, Action<string> onComplete = null)
        {
            if (action == null)
            {
                Debug.LogError("[InputManager] action이 null이라 리바인딩을 시작할 수 없습니다.");
                return null;
            }

            CancelActiveRebind();

            action.Disable();

            InputActionRebindingExtensions.RebindingOperation operation = action.PerformInteractiveRebinding(bindingIndex);

            for (int i = 0; i < _settings.RebindExcludePaths.Length; i++)
            {
                operation = operation.WithControlsExcluding(_settings.RebindExcludePaths[i]);
            }

            if (!string.IsNullOrEmpty(_settings.RebindCancelPath))
            {
                operation = operation.WithCancelingThrough(_settings.RebindCancelPath);
            }

            operation.OnComplete(op =>
            {
                try
                {
                    RestoreEnabledStateAfterRebind(action);
                    _activeRebind = null;
                    SaveBindings();
                    string effectivePath = action.bindings[bindingIndex].effectivePath;
                    SafeInvoke(OnRebindCompleted, action, bindingIndex, nameof(OnRebindCompleted));

                    try
                    {
                        onComplete?.Invoke(effectivePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[InputManager] StartRebind의 onComplete 콜백에서 예외가 발생했습니다: {e}");
                    }
                }
                finally
                {
                    // op.Dispose()를 반드시 실행하기 위해 finally에 둡니다 - 위 단계 중
                    // 하나라도 예외를 던지면 Dispose가 스킵되어 RebindingOperation이 새는
                    // 문제가 있었습니다. 또한 자기 자신의 OnComplete 콜백 안에서 바로
                    // Dispose하면 일부 Input System 버전에서 ObjectDisposedException이
                    // 보고된 바 있어(콜백 스택 안에서 자신을 정리하는 형태라서), 콜백
                    // 스택을 완전히 벗어난 다음 프레임으로 미룹니다.
                    _ = DisposeRebindOperationNextFrame(op);
                }
            });

            operation.OnCancel(op =>
            {
                try
                {
                    RestoreEnabledStateAfterRebind(action);
                    _activeRebind = null;
                    SafeInvoke(OnRebindCanceled, action, bindingIndex, nameof(OnRebindCanceled));
                }
                finally
                {
                    _ = DisposeRebindOperationNextFrame(op);
                }
            });

            _activeRebind = operation;
            operation.Start();
            SafeInvoke(OnRebindStarted, action, bindingIndex, nameof(OnRebindStarted));

            return operation;
        }

        // Gameplay 맵 소속 액션은, 리바인딩이 끝난 시점에도 여전히 팝업이 떠 있어
        // 게임플레이 입력이 막힌 상태라면 무조건 다시 켜면 안 됩니다 - Update()가
        // 관리하는 블로킹 상태와 어긋나서, 팝업이 떠 있는 동안에도 방금 리바인딩한
        // 액션 하나만 입력을 받아버리는 구멍이 있었습니다. UI 맵 소속 액션(리바인딩
        // 화면 자체를 여닫는 액션 등)은 항상 켜둔다는 클래스 설계를 그대로 따르므로
        // 이 검사에서 제외합니다.
        private void RestoreEnabledStateAfterRebind(InputAction action)
        {
            bool isGameplayAction = action.actionMap == _actions.Gameplay.Get();

            if (isGameplayAction && UIManager.Instance != null && UIManager.Instance.IsBlockingInput)
            {
                return;
            }

            action.Enable();
        }

        private async Awaitable DisposeRebindOperationNextFrame(InputActionRebindingExtensions.RebindingOperation op)
        {
            await Awaitable.NextFrameAsync();
            op.Dispose();
        }

        // EventBus.Publish와 동일한 패턴입니다: 구독자 하나가 예외를 던져도 나머지
        // 구독자에게는 정상적으로 전달되도록 각각 개별 try/catch로 격리합니다.
        private static void SafeInvoke<T1, T2>(Action<T1, T2> action, T1 arg1, T2 arg2, string eventName)
        {
            if (action == null)
            {
                return;
            }

            Delegate[] handlers = action.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<T1, T2>)handlers[i]).Invoke(arg1, arg2);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[InputManager] {eventName} 구독자에서 예외가 발생했습니다: {e}");
                }
            }
        }

        /// <summary>진행 중인 리바인딩이 있으면 취소합니다. 없으면 아무 일도 하지 않습니다.</summary>
        public void CancelActiveRebind()
        {
            _activeRebind?.Cancel();
        }

        public void ResetBinding(InputAction action, int bindingIndex)
        {
            if (action == null)
            {
                return;
            }

            action.RemoveBindingOverride(bindingIndex);
            SaveBindings();
        }

        public void ResetAllBindings()
        {
            _actions.RemoveAllBindingOverrides();
            SaveBindings();
        }

        public void SaveBindings()
        {
            // 앱 종료 중에는 매니저 종료 순서가 보장되지 않아 SaveManager.Instance가
            // 이미 null일 수 있습니다 (예: 리바인딩 완료 콜백이 SaveManager 종료 이후에 옴).
            if (SaveManager.Instance == null)
            {
                return;
            }

            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(InputSettingsKey);
            InputBindingOverridesData data = new InputBindingOverridesData { Json = _actions.SaveBindingOverridesAsJson() };
            SaveManager.Instance.Save(key, data);
            SaveManager.Instance.Flush();
        }

        public void LoadBindings()
        {
            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(InputSettingsKey);
            InputBindingOverridesData data = SaveManager.Instance.LoadOrCreate(key, () => new InputBindingOverridesData(), saveIfMissing: false);

            if (!string.IsNullOrEmpty(data.Json))
            {
                _actions.LoadBindingOverridesFromJson(data.Json);
            }
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            UnityEngine.InputSystem.InputSystem.onDeviceChange -= HandleDeviceChange;

            if (_actions == null)
            {
                return;
            }

            _actions.UI.Cancel.performed -= HandleCancelPerformed;
            _activeRebind?.Dispose();
            _actions.Dispose();
        }

        [Serializable]
        private sealed class InputBindingOverridesData
        {
            public string Json = "";
        }
    }
}
