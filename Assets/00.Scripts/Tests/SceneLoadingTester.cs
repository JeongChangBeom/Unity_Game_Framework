using System;
using System.Collections.Generic;
using System.Threading;
using GameFramework.SceneLoading;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class SceneLoadingTester : MonoBehaviour
    {
        [SerializeField] private string _sceneName = "TestScene2";
        [SerializeField] private string _missingSceneName = "이런씬은없음";
        [SerializeField] private string _addressableSceneAddress = "TestScene2";
        [SerializeField] private string _invalidAddressableAddress = "이런주소는없음";

        private string _log = "";
        private Vector2 _scroll;
        private int _progressEventCount;

        private void OnEnable()
        {
            SceneLoadingManager.Instance.OnSceneLoadStarted += HandleStarted;
            SceneLoadingManager.Instance.OnProgressChanged += HandleProgressChanged;
            SceneLoadingManager.Instance.OnSceneLoadCompleted += HandleCompleted;
            SceneLoadingManager.Instance.OnSceneLoadFailed += HandleFailed;
            SceneLoadingManager.Instance.OnSceneLoadAttemptFailed += HandleAttemptFailed;
            SceneLoadingManager.Instance.OnSceneLoadRetrying += HandleRetrying;
            SceneLoadingManager.Instance.OnSceneLoadFallback += HandleFallback;
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
            SceneLoadingManager.Instance.OnSceneLoadAttemptFailed -= HandleAttemptFailed;
            SceneLoadingManager.Instance.OnSceneLoadRetrying -= HandleRetrying;
            SceneLoadingManager.Instance.OnSceneLoadFallback -= HandleFallback;
        }

        private void HandleStarted(string sceneName) => Log($"이벤트: OnSceneLoadStarted({sceneName})");
        private void HandleProgressChanged(float progress) => _progressEventCount++;
        private void HandleCompleted(string sceneName) => Log($"이벤트: OnSceneLoadCompleted({sceneName})");
        private void HandleFailed(string sceneName) => Log($"이벤트: OnSceneLoadFailed({sceneName})");
        private void HandleAttemptFailed(string sceneName, int attempt, int maxAttempts) => Log($"이벤트: OnSceneLoadAttemptFailed({sceneName}, {attempt}/{maxAttempts})");
        private void HandleRetrying(string sceneName, int nextAttempt, int maxAttempts) => Log($"이벤트: OnSceneLoadRetrying({sceneName}, {nextAttempt}/{maxAttempts})");
        private void HandleFallback(string original, string fallback) => Log($"이벤트: OnSceneLoadFallback({original} -> {fallback})");

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
            GUILayout.Label("재시도/폴백을 확인하려면 SceneLoadingManagerSettings 에셋에서 MaxRetryCount/RetryDelaySeconds/FallbackSceneName을 설정한 뒤 아래 4번 버튼을 누르세요.");
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
            GUILayout.Label("Addressables");
            if (GUILayout.Button($"6) Load \"{_addressableSceneAddress}\" via Addressables"))
            {
                _ = sm.LoadSceneFromAddressableAsync(_addressableSceneAddress);
                Log($"요청: LoadSceneFromAddressableAsync(\"{_addressableSceneAddress}\")");
            }

            if (GUILayout.Button($"7) Load \"{_invalidAddressableAddress}\" via Addressables (에러 케이스)"))
            {
                _ = sm.LoadSceneFromAddressableAsync(_invalidAddressableAddress);
                Log($"요청: LoadSceneFromAddressableAsync(\"{_invalidAddressableAddress}\")");
            }

            GUILayout.Space(10);
            GUILayout.Label("Weighted Progress");
            if (GUILayout.Button("8) 가중 progress 테스트 (2개 가짜 프리로드 단계)"))
            {
                List<SceneLoadStep> steps = new List<SceneLoadStep>
                {
                    MakeFakeStep("프리로드A", 1f, 1f, fail: false),
                    MakeFakeStep("프리로드B", 2f, 2f, fail: false),
                };
                _ = sm.LoadSceneAsync(_sceneName, steps);
                Log($"요청: LoadSceneAsync(\"{_sceneName}\", [프리로드A(1초,가중치1), 프리로드B(2초,가중치2)])");
            }

            if (GUILayout.Button("9) 같은 테스트 + 한 단계 실패 (Critical=false, 씬 로드는 성공해야 함)"))
            {
                List<SceneLoadStep> steps = new List<SceneLoadStep>
                {
                    MakeFakeStep("프리로드A", 1f, 1f, fail: false),
                    MakeFakeStep("프리로드B(실패, Critical=false)", 1f, 1f, fail: true, critical: false),
                };
                _ = sm.LoadSceneAsync(_sceneName, steps);
                Log($"요청: LoadSceneAsync(\"{_sceneName}\", 한 단계 실패(Critical=false))");
            }

            if (GUILayout.Button("10) 같은 테스트 + 한 단계 실패 (Critical=true 기본값, 씬 로드 실패해야 함)"))
            {
                List<SceneLoadStep> steps = new List<SceneLoadStep>
                {
                    MakeFakeStep("프리로드A", 1f, 1f, fail: false),
                    MakeFakeStep("프리로드B(실패, Critical=true)", 1f, 1f, fail: true),
                };
                _ = sm.LoadSceneAsync(_sceneName, steps);
                Log($"요청: LoadSceneAsync(\"{_sceneName}\", 한 단계 실패(Critical=true 기본값))");
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

        // 진행률만 흉내 내는 가짜 프리로드 단계입니다. duration초 동안 report(0~1)을 매 프레임
        // 보고하다가, fail이면 마지막에 예외를 던집니다.
        private static SceneLoadStep MakeFakeStep(string label, float weight, float duration, bool fail, bool critical = true)
        {
            return new SceneLoadStep(label, weight, async (report, token) =>
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    report(Mathf.Clamp01(t / duration));
                    await Awaitable.NextFrameAsync(token);
                }

                if (fail)
                {
                    throw new Exception($"{label} 더미 실패");
                }
            }, critical);
        }

        private void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
