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
