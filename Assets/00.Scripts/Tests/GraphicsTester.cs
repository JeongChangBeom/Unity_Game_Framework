using GameFramework.Graphics;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class GraphicsTester : MonoBehaviour
    {
        private string _log = "";
        private Vector2 _scroll;

        private void OnEnable()
        {
            GraphicsManager.Instance.OnQualityLevelChanged += HandleQualityLevelChanged;
            GraphicsManager.Instance.OnTargetFrameRateChanged += HandleTargetFrameRateChanged;
            GraphicsManager.Instance.OnFullscreenChanged += HandleFullscreenChanged;
            GraphicsManager.Instance.OnVSyncChanged += HandleVSyncChanged;
            GraphicsManager.Instance.OnResolutionChanged += HandleResolutionChanged;
        }

        private void OnDisable()
        {
            if (GraphicsManager.Instance == null)
            {
                return;
            }

            GraphicsManager.Instance.OnQualityLevelChanged -= HandleQualityLevelChanged;
            GraphicsManager.Instance.OnTargetFrameRateChanged -= HandleTargetFrameRateChanged;
            GraphicsManager.Instance.OnFullscreenChanged -= HandleFullscreenChanged;
            GraphicsManager.Instance.OnVSyncChanged -= HandleVSyncChanged;
            GraphicsManager.Instance.OnResolutionChanged -= HandleResolutionChanged;
        }

        private void HandleQualityLevelChanged(int level) => Log($"OnQualityLevelChanged: {level}");
        private void HandleTargetFrameRateChanged(int fps) => Log($"OnTargetFrameRateChanged: {fps}");
        private void HandleFullscreenChanged(bool fullscreen) => Log($"OnFullscreenChanged: {fullscreen}");
        private void HandleVSyncChanged(bool enabled) => Log($"OnVSyncChanged: {enabled}");
        private void HandleResolutionChanged(int width, int height) => Log($"OnResolutionChanged: {width}x{height}");

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("Graphics Tester");

            GraphicsManager gm = GraphicsManager.Instance;

            GUILayout.Label($"QualityLevel={gm.QualityLevel} ({QualitySettings.names[Mathf.Clamp(gm.QualityLevel, 0, QualitySettings.names.Length - 1)]})");
            GUILayout.Label($"TargetFrameRate={gm.TargetFrameRate}, VSyncEnabled={gm.VSyncEnabled}");
            GUILayout.Label($"IsFullscreen={gm.IsFullscreen}, Resolution={gm.ResolutionWidth}x{gm.ResolutionHeight}");

            GUILayout.Space(10);
            GUILayout.Label("공통 (PC + 모바일)");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                int level = i;
                if (GUILayout.Button($"Quality {level}"))
                {
                    gm.SetQualityLevel(level);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("30 fps")) gm.SetTargetFrameRate(30);
            if (GUILayout.Button("60 fps")) gm.SetTargetFrameRate(60);
            if (GUILayout.Button("무제한 (-1)")) gm.SetTargetFrameRate(-1);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("PC 전용 (모바일에서는 효과 없음)");
            if (GUILayout.Button(gm.IsFullscreen ? "창모드로 전환" : "전체화면으로 전환"))
            {
                gm.SetFullscreen(!gm.IsFullscreen);
            }

            if (GUILayout.Button(gm.VSyncEnabled ? "VSync 끄기" : "VSync 켜기"))
            {
                gm.SetVSync(!gm.VSyncEnabled);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1280x720")) gm.SetResolution(1280, 720);
            if (GUILayout.Button("1920x1080")) gm.SetResolution(1920, 1080);
            GUILayout.EndHorizontal();

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

        private void Log(string msg)
        {
            string line = System.DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
