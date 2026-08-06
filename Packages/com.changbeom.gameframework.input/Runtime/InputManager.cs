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
            if (!UIManager.Instance.CurrentPopupCloseableByBackButton)
            {
                return;
            }

            UIManager.Instance.CloseTopPopup();
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            OnDeviceChange?.Invoke(device, change);
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
                action.Enable();
                SaveBindings();
                string effectivePath = action.bindings[bindingIndex].effectivePath;
                _activeRebind = null;
                OnRebindCompleted?.Invoke(action, bindingIndex);
                onComplete?.Invoke(effectivePath);
                op.Dispose();
            });

            operation.OnCancel(op =>
            {
                action.Enable();
                _activeRebind = null;
                OnRebindCanceled?.Invoke(action, bindingIndex);
                op.Dispose();
            });

            _activeRebind = operation;
            OnRebindStarted?.Invoke(action, bindingIndex);
            operation.Start();

            return operation;
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
