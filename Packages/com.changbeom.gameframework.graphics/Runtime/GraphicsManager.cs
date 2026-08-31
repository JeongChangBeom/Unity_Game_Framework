using System;
using GameFramework.Core;
using GameFramework.SaveLoad;
using UnityEngine;

namespace GameFramework.Graphics
{
    /// <summary>
    /// 디스플레이/화질 설정을 관리합니다. QualityLevel/TargetFrameRate는 PC와
    /// 모바일 모두에 의미가 있지만, Resolution/Fullscreen/VSync는 PC 전용입니다 -
    /// 모바일은 항상 전체화면에 해상도가 고정이라 이 세 설정을 호출해도 실질적인
    /// 효과가 없습니다(Unity API 자체가 안전하게 무시하므로 예외는 나지 않습니다).
    /// 어떤 설정 항목을 화면에 보여줄지는 게임이 만드는 Options UI가 플랫폼에 맞게
    /// 직접 판단하세요.
    /// </summary>
    public sealed class GraphicsManager : MonoSingleton<GraphicsManager>
    {
        private const string SettingsDomain = "settings";
        private const string GraphicsKey = "graphics";

        private GraphicsManagerSettings _settings;
        private GraphicsSaveData _data;

        public int QualityLevel => _data.QualityLevel;
        public int TargetFrameRate => _data.TargetFrameRate;
        public bool IsFullscreen => _data.IsFullscreen;
        public bool VSyncEnabled => _data.VSyncEnabled;
        public int ResolutionWidth => _data.ResolutionWidth;
        public int ResolutionHeight => _data.ResolutionHeight;

        public event Action<int> OnQualityLevelChanged;
        public event Action<int> OnTargetFrameRateChanged;
        public event Action<bool> OnFullscreenChanged;
        public event Action<bool> OnVSyncChanged;
        public event Action<int, int> OnResolutionChanged;

        protected override void OnInitialize()
        {
            _settings = LoadSettings();
            _data = LoadOrCreateData();
            ApplyAll();
        }

        private static GraphicsManagerSettings LoadSettings()
        {
            GraphicsManagerSettings settings = Resources.Load<GraphicsManagerSettings>(GraphicsManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[GraphicsManager] Resources/{GraphicsManagerSettings.ResourcePath}에서 GraphicsManagerSettings 에셋을 찾지 못했습니다. 기본값을 사용합니다. Assets/Create/Game Framework/Graphics/Graphics Manager Settings로 에셋을 만드세요.");
            return ScriptableObject.CreateInstance<GraphicsManagerSettings>();
        }

        private GraphicsSaveData LoadOrCreateData()
        {
            if (SaveManager.Instance == null)
            {
                return new GraphicsSaveData
                {
                    QualityLevel = _settings.DefaultQualityLevel,
                    TargetFrameRate = _settings.DefaultTargetFrameRate,
                    IsFullscreen = _settings.DefaultFullscreen,
                    VSyncEnabled = _settings.DefaultVSync,
                    ResolutionWidth = Screen.currentResolution.width,
                    ResolutionHeight = Screen.currentResolution.height,
                };
            }

            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(GraphicsKey);

            return SaveManager.Instance.LoadOrCreate(key, () => new GraphicsSaveData
            {
                QualityLevel = _settings.DefaultQualityLevel,
                TargetFrameRate = _settings.DefaultTargetFrameRate,
                IsFullscreen = _settings.DefaultFullscreen,
                VSyncEnabled = _settings.DefaultVSync,
                ResolutionWidth = Screen.currentResolution.width,
                ResolutionHeight = Screen.currentResolution.height,
            }, saveIfMissing: true);
        }

        private void ApplyAll()
        {
            _data.QualityLevel = Mathf.Clamp(_data.QualityLevel, 0, QualitySettings.names.Length - 1);
            _data.TargetFrameRate = ClampTargetFrameRate(_data.TargetFrameRate);
            _data.ResolutionWidth = Mathf.Max(1, _data.ResolutionWidth);
            _data.ResolutionHeight = Mathf.Max(1, _data.ResolutionHeight);

            QualitySettings.SetQualityLevel(_data.QualityLevel, true);
            Application.targetFrameRate = _data.TargetFrameRate;
            QualitySettings.vSyncCount = _data.VSyncEnabled ? 1 : 0;

            if (Screen.width != _data.ResolutionWidth || Screen.height != _data.ResolutionHeight || Screen.fullScreen != _data.IsFullscreen)
            {
                Screen.SetResolution(_data.ResolutionWidth, _data.ResolutionHeight, _data.IsFullscreen);
            }
        }

        /// <summary>-1(무제한)과 양수만 유효하게 취급하고, 0을 포함한 나머지는 -1로 보정합니다.</summary>
        private static int ClampTargetFrameRate(int fps)
        {
            if (fps == -1 || fps > 0)
            {
                return fps;
            }

            return -1;
        }

        public void SetQualityLevel(int level)
        {
            level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);

            _data.QualityLevel = level;
            QualitySettings.SetQualityLevel(level, true);
            QualitySettings.vSyncCount = _data.VSyncEnabled ? 1 : 0;

            Save();
            SafeInvoke(OnQualityLevelChanged, level, nameof(OnQualityLevelChanged));
        }

        public void SetTargetFrameRate(int fps)
        {
            fps = ClampTargetFrameRate(fps);

            _data.TargetFrameRate = fps;
            Application.targetFrameRate = fps;
            Save();
            SafeInvoke(OnTargetFrameRateChanged, fps, nameof(OnTargetFrameRateChanged));
        }

        /// <summary>PC 전용입니다. 모바일에서는 항상 전체화면이라 호출해도 효과가 없습니다.</summary>
        public void SetFullscreen(bool fullscreen)
        {
            _data.IsFullscreen = fullscreen;
            Screen.fullScreen = fullscreen;
            Save();
            SafeInvoke(OnFullscreenChanged, fullscreen, nameof(OnFullscreenChanged));
        }

        /// <summary>PC 전용입니다. 모바일에서는 효과가 없습니다.</summary>
        public void SetVSync(bool enabled)
        {
            _data.VSyncEnabled = enabled;
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            Save();
            SafeInvoke(OnVSyncChanged, enabled, nameof(OnVSyncChanged));
        }

        /// <summary>PC 전용입니다. 모바일에서는 해상도가 고정이라 효과가 없습니다.</summary>
        public void SetResolution(int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            _data.ResolutionWidth = width;
            _data.ResolutionHeight = height;
            Screen.SetResolution(width, height, _data.IsFullscreen);
            Save();
            SafeInvoke(OnResolutionChanged, width, height, nameof(OnResolutionChanged));
        }

        private void Save()
        {
            if (SaveManager.Instance == null)
            {
                return;
            }

            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(GraphicsKey);
            SaveManager.Instance.Save(key, _data);
            SaveManager.Instance.Flush();
        }

        private static void SafeInvoke<T>(Action<T> action, T arg, string eventName)
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
                    ((Action<T>)handlers[i]).Invoke(arg);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GraphicsManager] {eventName} 구독자에서 예외가 발생했습니다: {e}");
                }
            }
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
                    Debug.LogError($"[GraphicsManager] {eventName} 구독자에서 예외가 발생했습니다: {e}");
                }
            }
        }

        [Serializable]
        private sealed class GraphicsSaveData
        {
            public int QualityLevel;
            public int TargetFrameRate;
            public bool IsFullscreen;
            public bool VSyncEnabled;
            public int ResolutionWidth;
            public int ResolutionHeight;
        }
    }
}
