using System;
using System.Collections.Generic;
using System.Threading;
using GameFramework.Core;
using GameFramework.UISystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 비동기 씬 전환과, 최소 노출시간이 보장되는 로딩 화면을 관리합니다. 로딩 화면은
    /// UIManager.OverlayRoot 위에 표시됩니다.
    /// </summary>
    public sealed class SceneLoadingManager : MonoSingleton<SceneLoadingManager>
    {
        private SceneLoadingManagerSettings _settings;
        private SceneLoadingScreenBase _loadingScreen;

        public bool IsLoading { get; private set; }
        public float Progress { get; private set; }
        public string CurrentSceneName { get; private set; }

        public event Action<string> OnSceneLoadStarted;
        public event Action<float> OnProgressChanged;
        public event Action<string> OnSceneLoadCompleted;
        public event Action<string> OnSceneLoadFailed;

        protected override void OnInitialize()
        {
            _settings = LoadSettings();
            _loadingScreen = CreateLoadingScreen();
        }

        private static SceneLoadingManagerSettings LoadSettings()
        {
            SceneLoadingManagerSettings settings = Resources.Load<SceneLoadingManagerSettings>(SceneLoadingManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[SceneLoadingManager] Resources/{SceneLoadingManagerSettings.ResourcePath}에서 SceneLoadingManagerSettings 에셋을 찾지 못했습니다. 기본값을 사용합니다. Assets/Create/Game Framework/Scene Loading/Scene Loading Manager Settings로 에셋을 만드세요.");
            return ScriptableObject.CreateInstance<SceneLoadingManagerSettings>();
        }

        private SceneLoadingScreenBase CreateLoadingScreen()
        {
            Transform overlayRoot = UIManager.Instance.OverlayRoot;

            SceneLoadingScreenBase screen = _settings.LoadingScreenPrefabOverride != null
                ? Instantiate(_settings.LoadingScreenPrefabOverride, overlayRoot)
                : DefaultSceneLoadingScreen.Create(overlayRoot);

            screen.ApplySettings(_settings.FadeDuration);
            screen.gameObject.SetActive(false);

            return screen;
        }

        /// <summary>지정한 씬으로 전환합니다 (LoadSceneMode.Single). 이미 로딩 중이면 무시됩니다.</summary>
        public async Awaitable LoadSceneAsync(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoadingManager] 이미 씬을 로딩 중이라 요청을 무시합니다: {sceneName}");
                return;
            }

            // IsLoading = true 자체는 예외를 던질 수 없는 단순 대입이라 try 밖에 둡니다.
            // 그 다음부터는 OnProgressChanged/OnSceneLoadStarted 이벤트 발행을 포함해
            // 전부 예외를 던질 수 있으므로(구독자가 던지는 경우 포함), 반드시 try 안에
            // 있어야 finally에서 IsLoading을 복구할 수 있습니다. UIManager.CloseAll이나
            // 사용자가 작성하는 ISceneEntryPoint/ISceneExitPoint, 앱 종료로 인한
            // destroyCancellationToken 취소까지 - 이 메서드 본문 어디서든 예외가 날 수
            // 있습니다. try/finally 없이 예외가 그대로 튀어나가면 IsLoading이 영원히
            // true로 남아 위쪽 가드 때문에 이후 어떤 씬도 다시 로드되지 않는 상태가 됩니다.
            IsLoading = true;
            bool screenShown = false;

            try
            {
                CurrentSceneName = sceneName;
                SetProgress(0f);
                OnSceneLoadStarted?.Invoke(sceneName);

                // 씬 전환 전 팝업/토스트 대기열까지 정리 (UIManager.CloseAll이 이미 이 용도로 문서화되어 있음)
                UIManager.Instance.CloseAll();

                await ShowLoadingScreenAsync();
                screenShown = true;

                float startTime = Time.unscaledTime;

                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op == null)
                {
                    Debug.LogError($"[SceneLoadingManager] 씬을 로드하지 못했습니다 (Build Settings에 등록되지 않았을 수 있습니다): {sceneName}");
                    CurrentSceneName = SceneManager.GetActiveScene().name;
                    OnSceneLoadFailed?.Invoke(sceneName);
                    return;
                }

                op.allowSceneActivation = false;

                // 로드가 실제로 시작된 뒤에만(위에서 op == null이면 이미 반환됨) 지금 씬을 떠난다고
                // 확정할 수 있으므로, 이 시점에 지금 씬의 ISceneExitPoint를 실행합니다. 로딩
                // 화면이 이미 화면을 덮은 뒤라 여기서 하는 정리 작업은 사용자에게 보이지 않습니다.
                await RunExitPointsAsync();

                while (op.progress < 0.9f)
                {
                    SetProgress(op.progress / 0.9f);
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                SetProgress(1f);

                // 로딩 화면이 최소 이 시간만큼은 떠 있도록, 이미 지난 시간을 뺀 나머지만 기다립니다.
                // 빠른 로드에서 화면이 순간적으로 깜빡이고 사라지는 것을 방지합니다.
                float remain = _settings.MinimumLoadingScreenDuration - (Time.unscaledTime - startTime);
                if (remain > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(remain, destroyCancellationToken);
                }

                op.allowSceneActivation = true;

                while (!op.isDone)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                // 새 씬이 이제 활성 씬입니다. 이 씬의 ISceneEntryPoint가 전부 끝날 때까지
                // 로딩 화면을 유지해서, 플레이어 스폰/씬 전용 데이터 로드 등이 실제로
                // 끝난 뒤에야 화면이 사라지도록 합니다.
                await RunEntryPointsAsync();

                OnSceneLoadCompleted?.Invoke(sceneName);
            }
            catch (OperationCanceledException)
            {
                // 앱 종료 등으로 destroyCancellationToken이 취소된 정상적인 상황이라 에러로
                // 남기지 않습니다.
                OnSceneLoadFailed?.Invoke(sceneName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoadingManager] {sceneName} 로드 중 예외가 발생했습니다: {e}");
                OnSceneLoadFailed?.Invoke(sceneName);
            }
            finally
            {
                if (screenShown)
                {
                    try
                    {
                        await HideLoadingScreenAsync();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SceneLoadingManager] 로딩 화면을 숨기지 못했습니다: {e}");
                    }
                }

                IsLoading = false;
            }
        }

        private async Awaitable RunExitPointsAsync()
        {
            List<ISceneExitPoint> points = FindInActiveScene<ISceneExitPoint>();

            for (int i = 0; i < points.Count; i++)
            {
                ISceneExitPoint point = points[i];
                await RunHookWithTimeoutAsync(point.OnSceneExitAsync, _settings.EntryExitPointTimeoutSeconds,
                    destroyCancellationToken, $"ISceneExitPoint({point.GetType().Name})");
            }
        }

        private async Awaitable RunEntryPointsAsync()
        {
            List<ISceneEntryPoint> points = FindInActiveScene<ISceneEntryPoint>();
            points.Sort((a, b) => a.Order.CompareTo(b.Order));

            for (int i = 0; i < points.Count; i++)
            {
                ISceneEntryPoint point = points[i];
                await RunHookWithTimeoutAsync(point.OnSceneEnterAsync, _settings.EntryExitPointTimeoutSeconds,
                    destroyCancellationToken, $"ISceneEntryPoint({point.GetType().Name})");
            }
        }

        // 사용자 코드(ISceneEntryPoint/ISceneExitPoint)가 반환한 Awaitable이 예외 없이 그냥
        // 영원히 끝나지 않으면, try/finally만으로는 IsLoading을 되돌릴 방법이 없습니다(finally는
        // 예외 또는 정상 반환에서만 실행됨). 그래서 훅을 타임아웃과 경합시켜, 타임아웃이 먼저
        // 끝나면 훅을 더 기다리지 않고 다음 단계로 넘어갑니다 - 훅 자체는 백그라운드에서 계속
        // 실행되도록 두고(취소할 방법이 없으므로), 그 결과는 더 이상 씬 전환 파이프라인을
        // 막지 않게만 합니다.
        private static async Awaitable RunHookWithTimeoutAsync(Func<Awaitable> hook, float timeoutSeconds,
            CancellationToken destroyToken, string hookLabel)
        {
            if (timeoutSeconds <= 0f)
            {
                await hook();
                return;
            }

            AwaitableCompletionSource tcs = new AwaitableCompletionSource();
            bool settled = false;

            async Awaitable RunHookAsync()
            {
                try
                {
                    await hook();
                    if (!settled)
                    {
                        settled = true;
                        tcs.SetResult();
                    }
                }
                catch (Exception e)
                {
                    if (!settled)
                    {
                        settled = true;
                        tcs.SetException(e);
                    }
                    else
                    {
                        Debug.LogError($"[SceneLoadingManager] 타임아웃으로 건너뛴 {hookLabel}에서 나중에 예외가 발생했습니다: {e}");
                    }
                }
            }

            async Awaitable RunTimeoutAsync()
            {
                try
                {
                    await Awaitable.WaitForSecondsAsync(timeoutSeconds, destroyToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!settled)
                {
                    settled = true;
                    Debug.LogError($"[SceneLoadingManager] {hookLabel}가 {timeoutSeconds}초 안에 끝나지 않아 건너뛰고 계속 진행합니다.");
                    tcs.SetResult();
                }
            }

            _ = RunHookAsync();
            _ = RunTimeoutAsync();

            await tcs.Awaitable;
        }

        // 등록 절차 없이, 활성 씬의 루트 오브젝트들을 훑어서 해당 인터페이스를 구현한
        // 컴포넌트를 전부 찾습니다. GetComponentsInChildren<T>는 T가 인터페이스여도 동작합니다.
        // 비활성화된(SetActive(false)) 오브젝트는 씬 작성자가 의도적으로 꺼둔 것이므로
        // 건너뜁니다 - 예를 들어 나중에 쓰려고 비활성 상태로 남겨둔 오브젝트에 훅이 붙어
        // 있다고 해서 그게 매번 호출되면 안 됩니다.
        private static List<T> FindInActiveScene<T>() where T : class
        {
            List<T> result = new List<T>();
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                if (!roots[i].activeInHierarchy)
                {
                    continue;
                }

                T[] found = roots[i].GetComponentsInChildren<T>(false);
                result.AddRange(found);
            }

            return result;
        }

        /// <summary>생성된 ESceneKey로 씬을 전환합니다.</summary>
        public async Awaitable LoadSceneAsync(ESceneKey sceneKey)
        {
            if (sceneKey == ESceneKey.None)
            {
                Debug.LogError("[SceneLoadingManager] ESceneKey.None으로는 씬을 로드할 수 없습니다.");
                OnSceneLoadFailed?.Invoke(sceneKey.ToString());
                return;
            }

            await LoadSceneAsync(sceneKey.ToString());
        }

        private void SetProgress(float value)
        {
            Progress = value;
            _loadingScreen.SetProgress(value);
            OnProgressChanged?.Invoke(value);
        }

        private Awaitable ShowLoadingScreenAsync()
        {
            AwaitableCompletionSource tcs = new AwaitableCompletionSource();
            _loadingScreen.RequestShow(() => tcs.SetResult());
            return tcs.Awaitable;
        }

        private Awaitable HideLoadingScreenAsync()
        {
            AwaitableCompletionSource tcs = new AwaitableCompletionSource();
            _loadingScreen.RequestHide(() => tcs.SetResult());
            return tcs.Awaitable;
        }
    }
}
