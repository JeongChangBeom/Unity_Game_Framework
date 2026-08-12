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
            // 저장된 QualityLevel이 지금 프로젝트의 Quality Settings 단계 수보다 클
            // 수 있습니다 (예: 예전엔 6단계였다가 프로젝트에서 3단계로 줄인 경우).
            // Unity가 알아서 클램프해줄 수도 있지만, 우리가 노출하는 QualityLevel
            // getter가 실제 적용된 값과 어긋나지 않도록 여기서 직접 보정합니다.
            _data.QualityLevel = Mathf.Clamp(_data.QualityLevel, 0, QualitySettings.names.Length - 1);

            QualitySettings.SetQualityLevel(_data.QualityLevel, true);
            Application.targetFrameRate = _data.TargetFrameRate;
            QualitySettings.vSyncCount = _data.VSyncEnabled ? 1 : 0;

            // 이미 그 해상도/모드로 떠 있으면(대부분의 경우) 굳이 다시 호출하지 않습니다 -
            // 매 부팅마다 불필요하게 창을 다시 그리는 깜빡임을 방지합니다.
            if (Screen.width != _data.ResolutionWidth || Screen.height != _data.ResolutionHeight || Screen.fullScreen != _data.IsFullscreen)
            {
                Screen.SetResolution(_data.ResolutionWidth, _data.ResolutionHeight, _data.IsFullscreen);
            }
        }

        public void SetQualityLevel(int level)
        {
            level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);

            _data.QualityLevel = level;
            QualitySettings.SetQualityLevel(level, true);

            // Quality Level 프리셋 자체가 자기만의 VSync 값을 갖고 있어서, 위 호출이
            // 우리가 따로 저장해둔 VSyncEnabled를 조용히 덮어쓸 수 있습니다. 사용자가
            // 선택한 VSync 값을 다시 강제로 맞춰줍니다.
            QualitySettings.vSyncCount = _data.VSyncEnabled ? 1 : 0;

            Save();
            OnQualityLevelChanged?.Invoke(level);
        }

        public void SetTargetFrameRate(int fps)
        {
            _data.TargetFrameRate = fps;
            Application.targetFrameRate = fps;
            Save();
            OnTargetFrameRateChanged?.Invoke(fps);
        }

        /// <summary>PC 전용입니다. 모바일에서는 항상 전체화면이라 호출해도 효과가 없습니다.</summary>
        public void SetFullscreen(bool fullscreen)
        {
            _data.IsFullscreen = fullscreen;
            Screen.fullScreen = fullscreen;
            Save();
            OnFullscreenChanged?.Invoke(fullscreen);
        }

        /// <summary>PC 전용입니다. 모바일에서는 효과가 없습니다.</summary>
        public void SetVSync(bool enabled)
        {
            _data.VSyncEnabled = enabled;
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            Save();
            OnVSyncChanged?.Invoke(enabled);
        }

        /// <summary>PC 전용입니다. 모바일에서는 해상도가 고정이라 효과가 없습니다.</summary>
        public void SetResolution(int width, int height)
        {
            _data.ResolutionWidth = width;
            _data.ResolutionHeight = height;
            Screen.SetResolution(width, height, _data.IsFullscreen);
            Save();
            OnResolutionChanged?.Invoke(width, height);
        }

        private void Save()
        {
            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(GraphicsKey);
            SaveManager.Instance.Save(key, _data);
            SaveManager.Instance.Flush();
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
