using System;
using GameFramework.SceneLoading;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class SceneLoadingTester : MonoBehaviour
    {
        [SerializeField] private string _sceneName = "TestScene2";
        [SerializeField] private string _missingSceneName = "이런씬은없음";

        private string _log = "";
        private Vector2 _scroll;
        private int _progressEventCount;

        private void OnEnable()
        {
            SceneLoadingManager.Instance.OnSceneLoadStarted += HandleStarted;
            SceneLoadingManager.Instance.OnProgressChanged += HandleProgressChanged;
            SceneLoadingManager.Instance.OnSceneLoadCompleted += HandleCompleted;
            SceneLoadingManager.Instance.OnSceneLoadFailed += HandleFailed;
        }

        private void OnDisable()
        {
            if (SceneLoadingManager.Instance == null)
            {
                return;
            }

            SceneLoadingManager.Instance.OnSceneLoadStarted -= HandleStarted;
            SceneLoadingManager.Instance.OnProgressChanged -= HandleProgressChanged;
            SceneLoadingManager.Instance.OnSceneLoadCompleted -= HandleCompleted;
            SceneLoadingManager.Instance.OnSceneLoadFailed -= HandleFailed;
        }

        private void HandleStarted(string sceneName) => Log($"이벤트: OnSceneLoadStarted({sceneName})");
        private void HandleProgressChanged(float progress) => _progressEventCount++;
        private void HandleCompleted(string sceneName) => Log($"이벤트: OnSceneLoadCompleted({sceneName})");
        private void HandleFailed(string sceneName) => Log($"이벤트: OnSceneLoadFailed({sceneName})");

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("Scene Loading Tester");

            SceneLoadingManager sm = SceneLoadingManager.Instance;
            GUILayout.Label($"IsLoading={sm.IsLoading}, Progress={sm.Progress:0.00}, CurrentSceneName={sm.CurrentSceneName}");
            GUILayout.Label($"OnProgressChanged 발행 횟수={_progressEventCount}");

            GUILayout.Space(10);
            GUILayout.Label("Load");
            if (GUILayout.Button($"1) Load \"{_sceneName}\" (string)"))
            {
                _ = sm.LoadSceneAsync(_sceneName);
                Log($"요청: LoadSceneAsync(\"{_sceneName}\")");
            }

            if (GUILayout.Button("2) Load ESceneKey.TestScene2 (ESceneKey 정상 케이스)"))
            {
                _ = sm.LoadSceneAsync(ESceneKey.TestScene2);
                Log("요청: LoadSceneAsync(ESceneKey.TestScene2)");
            }

            if (GUILayout.Button("3) Load ESceneKey.None (에러 로그 확인용)"))
            {
                _ = sm.LoadSceneAsync(ESceneKey.None);
                Log("요청: LoadSceneAsync(ESceneKey.None)");
            }

            GUILayout.Space(10);
            GUILayout.Label("Error Cases");
            if (GUILayout.Button($"4) Load \"{_missingSceneName}\" (Build Settings에 없는 씬)"))
            {
                _ = sm.LoadSceneAsync(_missingSceneName);
                Log($"요청: LoadSceneAsync(\"{_missingSceneName}\")");
            }

            if (GUILayout.Button("5) 연속 두 번 빠르게 호출 (IsLoading 가드 확인)"))
            {
                _ = sm.LoadSceneAsync(_sceneName);
                _ = sm.LoadSceneAsync(_sceneName);
                Log($"요청: LoadSceneAsync(\"{_sceneName}\") x2 연속 호출 (두 번째는 무시되어야 함)");
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

        private void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
