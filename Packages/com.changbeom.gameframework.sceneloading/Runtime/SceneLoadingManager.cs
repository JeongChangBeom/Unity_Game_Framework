using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameFramework.Core;
using GameFramework.UISystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 비동기 씬 전환과, 최소 노출시간이 보장되는 로딩 화면을 관리합니다. 로딩 화면은
    /// UIManager.OverlayRoot 위에 표시됩니다. Build Settings에 등록된 씬(LoadSceneAsync)과
    /// Addressables로 등록된 씬(LoadSceneFromAddressableAsync)을 모두 지원합니다.
    /// </summary>
    public sealed class SceneLoadingManager : MonoSingleton<SceneLoadingManager>
    {
        private SceneLoadingManagerSettings _settings;
        private SceneLoadingScreenBase _loadingScreen;
        private AsyncOperationHandle<SceneInstance>? _activeAddressableSceneHandle;

        public bool IsLoading { get; private set; }
        public float Progress { get; private set; }
        public string CurrentSceneName { get; private set; }

        public event Action<string> OnSceneLoadStarted;
        public event Action<float> OnProgressChanged;
        public event Action<string> OnSceneLoadCompleted;

        /// <summary>재시도/폴백까지 전부 소진된 뒤 최종 실패했을 때만 발행됩니다.</summary>
        public event Action<string> OnSceneLoadFailed;

        /// <summary>개별 시도 하나가 실패할 때마다 발행됩니다 (sceneOrAddress, 시도 번호, 최대 시도 횟수).</summary>
        public event Action<string, int, int> OnSceneLoadAttemptFailed;

        /// <summary>재시도 대기 시간이 끝나고 다음 시도를 시작하기 직전에 발행됩니다 (sceneOrAddress, 다음 시도 번호, 최대 시도 횟수).</summary>
        public event Action<string, int, int> OnSceneLoadRetrying;

        /// <summary>원래 요청의 재시도가 모두 소진되어 폴백 씬으로 전환을 시작할 때 발행됩니다 (원래 요청, 폴백 씬 이름).</summary>
        public event Action<string, string> OnSceneLoadFallback;

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

        /// <summary>지정한 씬으로 전환합니다 (LoadSceneMode.Single, Build Settings 기준). 이미 로딩 중이면 무시됩니다.</summary>
        public async Awaitable LoadSceneAsync(string sceneName)
        {
            await LoadSceneAsync(sceneName, null);
        }

        /// <summary>
        /// 지정한 씬으로 전환하면서, extraSteps로 전달한 가중치 있는 추가 작업(예: 다음 씬에 필요한
        /// 에셋 프리로드)을 씬 로드와 동시에 진행하고 그 진행률까지 Progress에 가중 합산합니다.
        /// </summary>
        public async Awaitable LoadSceneAsync(string sceneName, IReadOnlyList<SceneLoadStep> extraSteps)
        {
            await LoadSceneCoreAsync(SceneLoadRequest.FromBuildSettings(sceneName), extraSteps);
        }

        /// <summary>Addressables 주소로 씬을 전환합니다 (LoadSceneMode.Single). 이미 로딩 중이면 무시됩니다.</summary>
        public async Awaitable LoadSceneFromAddressableAsync(string address)
        {
            await LoadSceneFromAddressableAsync(address, null);
        }

        /// <summary>Addressables 주소로 씬을 전환하면서, extraSteps의 진행률까지 Progress에 가중 합산합니다.</summary>
        public async Awaitable LoadSceneFromAddressableAsync(string address, IReadOnlyList<SceneLoadStep> extraSteps)
        {
            await LoadSceneCoreAsync(SceneLoadRequest.FromAddressable(address), extraSteps);
        }

        private async Awaitable LoadSceneCoreAsync(SceneLoadRequest request, IReadOnlyList<SceneLoadStep> extraSteps)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoadingManager] 이미 씬을 로딩 중이라 요청을 무시합니다: {request.Label}");
                return;
            }

            IsLoading = true;
            bool screenShown = false;

            try
            {
                CurrentSceneName = request.Label;
                SetProgress(0f);
                OnSceneLoadStarted?.Invoke(request.Label);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.CloseAll();
                }

                await RunWithTimeoutAsync(ShowLoadingScreenAsync, _settings.LoadingScreenTimeoutSeconds,
                    destroyCancellationToken, "로딩 화면 표시(RequestShow)", treatTimeoutAsSuccess: true);
                screenShown = true;

                LoadPipelineState state = new LoadPipelineState { StartTime = Time.unscaledTime };
                bool succeeded = await RunAttemptsWithRetryAsync(request, extraSteps, state, isFallback: false);

                if (!succeeded)
                {
                    string fallbackSceneName = _settings.FallbackSceneName;
                    if (!string.IsNullOrWhiteSpace(fallbackSceneName))
                    {
                        if (fallbackSceneName != request.Label)
                        {
                            OnSceneLoadFallback?.Invoke(request.Label, fallbackSceneName);
                            succeeded = await RunAttemptsWithRetryAsync(
                                SceneLoadRequest.FromBuildSettings(fallbackSceneName), null, state, isFallback: true);
                        }
                        else
                        {
                            Debug.LogWarning($"[SceneLoadingManager] FallbackSceneName이 실패한 요청({request.Label})과 같아 폴백을 건너뜁니다.");
                        }
                    }
                }

                if (!succeeded)
                {
                    OnSceneLoadFailed?.Invoke(request.Label);
                }
            }
            catch (OperationCanceledException)
            {
                OnSceneLoadFailed?.Invoke(request.Label);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoadingManager] {request.Label} 로드 파이프라인에서 예상치 못한 예외가 발생했습니다: {e}");
                OnSceneLoadFailed?.Invoke(request.Label);
            }
            finally
            {
                if (screenShown)
                {
                    try
                    {
                        await RunWithTimeoutAsync(HideLoadingScreenAsync, _settings.LoadingScreenTimeoutSeconds,
                            destroyCancellationToken, "로딩 화면 숨김(RequestHide)", treatTimeoutAsSuccess: true);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SceneLoadingManager] 로딩 화면을 숨기지 못했습니다: {e}");
                    }
                }

                IsLoading = false;
            }
        }

        private async Awaitable<bool> RunAttemptsWithRetryAsync(SceneLoadRequest request, IReadOnlyList<SceneLoadStep> extraSteps,
            LoadPipelineState state, bool isFallback)
        {
            int maxAttempts = 1 + Mathf.Max(0, isFallback ? _settings.FallbackMaxRetryCount : _settings.MaxRetryCount);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await RunSingleAttemptAsync(request, extraSteps, state);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SceneLoadingManager] {request.Label} 로드 시도 {attempt}/{maxAttempts} 실패: {e}");
                    OnSceneLoadAttemptFailed?.Invoke(request.Label, attempt, maxAttempts);

                    if (attempt >= maxAttempts)
                    {
                        return false;
                    }

                    if (_settings.RetryDelaySeconds > 0f)
                    {
                        await Awaitable.WaitForSecondsAsync(_settings.RetryDelaySeconds, destroyCancellationToken);
                    }

                    OnSceneLoadRetrying?.Invoke(request.Label, attempt + 1, maxAttempts);
                }
            }

            return false;
        }

        private async Awaitable RunSingleAttemptAsync(SceneLoadRequest request, IReadOnlyList<SceneLoadStep> extraSteps, LoadPipelineState state)
        {
            CurrentSceneName = request.Label;
            SetProgress(0f);

            ISceneLoadOperation op = request.Begin(_settings.SceneOperationTimeoutSeconds);
            if (op.FailedToStart)
            {
                CurrentSceneName = SceneManager.GetActiveScene().name;
                throw new InvalidOperationException(
                    $"씬을 로드하지 못했습니다 (Build Settings에 등록되지 않았을 수 있습니다): {request.Label}");
            }

            if (!state.ExitPointsRan)
            {
                await RunExitPointsAsync();
                state.ExitPointsRan = true;
            }

            bool activated = false;
            bool succeeded = false;

            try
            {
                List<LoadContributor> contributors = new List<LoadContributor> { new LoadContributor(request.Label, 1f) };
                _ = RunSceneReadyContributorAsync(op, contributors[0], _settings.SceneOperationTimeoutSeconds, destroyCancellationToken);

                if (extraSteps != null)
                {
                    for (int i = 0; i < extraSteps.Count; i++)
                    {
                        SceneLoadStep step = extraSteps[i];
                        bool critical = step.RunAsync == null || step.Critical;
                        LoadContributor contributor = new LoadContributor(step.Label, step.Weight, critical);
                        contributors.Add(contributor);
                        _ = RunExtraStepContributorAsync(step, contributor, _settings.LoadStepTimeoutSeconds, destroyCancellationToken);
                    }
                }

                while (!AllDone(contributors))
                {
                    SetProgress(ComputeWeightedProgress(contributors));
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                ThrowIfCriticalFailure(contributors);
                SetProgress(1f);

                float remain = _settings.MinimumLoadingScreenDuration - (Time.unscaledTime - state.StartTime);
                if (remain > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(remain, destroyCancellationToken);
                }

                op.Activate();
                activated = true;

                await RunWithTimeoutAsync(() => op.WaitUntilActivationDoneAsync(destroyCancellationToken),
                    _settings.SceneOperationTimeoutSeconds, destroyCancellationToken,
                    $"\"{request.Label}\" 활성화 마무리", treatTimeoutAsSuccess: true);

                TrackAddressableSceneHandle(op);

                await RunEntryPointsAsync();

                OnSceneLoadCompleted?.Invoke(request.Label);
                succeeded = true;
            }
            finally
            {
                if (activated && !succeeded)
                {
                    Debug.LogError($"[SceneLoadingManager] \"{request.Label}\" 씬은 이미 활성화됐지만 이후 단계(ISceneEntryPoint 등)에서 실패했습니다. 이전 씬은 이미 언로드되어 되돌릴 수 없어, 지금 이 씬이 깨진 상태로 계속 활성 씬입니다. MaxRetryCount/FallbackSceneName을 설정해두면 다음 시도가 이 씬을 자동으로 정리합니다.");
                }

                if (!activated)
                {
                    try
                    {
                        await op.ReleaseOnFailure();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SceneLoadingManager] 실패한 로드를 정리하지 못했습니다: {e}");
                    }

                    CurrentSceneName = SceneManager.GetActiveScene().name;
                }
            }
        }

        private void TrackAddressableSceneHandle(ISceneLoadOperation op)
        {
            if (_activeAddressableSceneHandle.HasValue)
            {
                _ = Addressables.UnloadSceneAsync(_activeAddressableSceneHandle.Value);
                _activeAddressableSceneHandle = null;
            }

            if (op is AddressableSceneLoadOperation addressableOp)
            {
                _activeAddressableSceneHandle = addressableOp.Handle;
            }
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            if (_activeAddressableSceneHandle.HasValue)
            {
                Addressables.Release(_activeAddressableSceneHandle.Value);
                _activeAddressableSceneHandle = null;
            }
        }

        private async Awaitable RunExitPointsAsync()
        {
            List<ISceneExitPoint> points = FindInActiveScene<ISceneExitPoint>();

            for (int i = 0; i < points.Count; i++)
            {
                ISceneExitPoint point = points[i];
                await RunWithTimeoutAsync(point.OnSceneExitAsync, _settings.EntryExitPointTimeoutSeconds,
                    destroyCancellationToken, $"ISceneExitPoint({point.GetType().Name})", treatTimeoutAsSuccess: true);
            }
        }

        private async Awaitable RunEntryPointsAsync()
        {
            List<ISceneEntryPoint> points = FindInActiveScene<ISceneEntryPoint>();
            points.Sort((a, b) => a.Order.CompareTo(b.Order));

            for (int i = 0; i < points.Count; i++)
            {
                ISceneEntryPoint point = points[i];
                await RunWithTimeoutAsync(point.OnSceneEnterAsync, _settings.EntryExitPointTimeoutSeconds,
                    destroyCancellationToken, $"ISceneEntryPoint({point.GetType().Name})", treatTimeoutAsSuccess: true);
            }
        }

        private static async Awaitable RunWithTimeoutAsync(Func<Awaitable> action, float timeoutSeconds,
            CancellationToken destroyToken, string label, bool treatTimeoutAsSuccess)
        {
            if (timeoutSeconds <= 0f)
            {
                await action();
                return;
            }

            AwaitableCompletionSource tcs = new AwaitableCompletionSource();
            bool settled = false;

            async Awaitable RunActionAsync()
            {
                try
                {
                    await action();
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
                        Debug.LogError($"[SceneLoadingManager] 타임아웃으로 건너뛴 {label}에서 나중에 예외가 발생했습니다: {e}");
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
                    if (treatTimeoutAsSuccess)
                    {
                        Debug.LogError($"[SceneLoadingManager] {label}가 {timeoutSeconds}초 안에 끝나지 않아 건너뛰고 계속 진행합니다.");
                        tcs.SetResult();
                    }
                    else
                    {
                        tcs.SetException(new TimeoutException($"{label}가 {timeoutSeconds}초 안에 끝나지 않았습니다."));
                    }
                }
            }

            _ = RunActionAsync();
            _ = RunTimeoutAsync();

            await tcs.Awaitable;
        }

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

        private sealed class LoadPipelineState
        {
            public float StartTime;
            public bool ExitPointsRan;
        }

        private readonly struct SceneLoadRequest
        {
            public string Label { get; }
            private readonly bool _isAddressable;

            private SceneLoadRequest(string label, bool isAddressable)
            {
                Label = label;
                _isAddressable = isAddressable;
            }

            public static SceneLoadRequest FromBuildSettings(string sceneName) => new SceneLoadRequest(sceneName, false);
            public static SceneLoadRequest FromAddressable(string address) => new SceneLoadRequest(address, true);

            public ISceneLoadOperation Begin(float cleanupTimeoutSeconds) => _isAddressable
                ? new AddressableSceneLoadOperation(Label, cleanupTimeoutSeconds)
                : (ISceneLoadOperation)new BuildSettingsSceneLoadOperation(Label, cleanupTimeoutSeconds);
        }

        private interface ISceneLoadOperation
        {
            bool FailedToStart { get; }
            Awaitable WaitUntilReadyAsync(Action<float> onProgress, CancellationToken token);
            void Activate();
            Awaitable WaitUntilActivationDoneAsync(CancellationToken token);
            Awaitable ReleaseOnFailure();
        }

        private sealed class BuildSettingsSceneLoadOperation : ISceneLoadOperation
        {
            private readonly AsyncOperation _op;
            private readonly float _cleanupTimeoutSeconds;

            public BuildSettingsSceneLoadOperation(string sceneName, float cleanupTimeoutSeconds)
            {
                _cleanupTimeoutSeconds = cleanupTimeoutSeconds;
                _op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (_op != null)
                {
                    _op.allowSceneActivation = false;
                }
            }

            public bool FailedToStart => _op == null;

            public async Awaitable WaitUntilReadyAsync(Action<float> onProgress, CancellationToken token)
            {
                while (_op.progress < 0.9f)
                {
                    onProgress(_op.progress / 0.9f);
                    await Awaitable.NextFrameAsync(token);
                }

                onProgress(1f);
            }

            public void Activate() => _op.allowSceneActivation = true;

            public async Awaitable WaitUntilActivationDoneAsync(CancellationToken token)
            {
                while (!_op.isDone)
                {
                    await Awaitable.NextFrameAsync(token);
                }

                Scene newScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

                List<Scene> previousScenes = new List<Scene>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene != newScene)
                    {
                        previousScenes.Add(scene);
                    }
                }

                SceneManager.SetActiveScene(newScene);

                for (int i = 0; i < previousScenes.Count; i++)
                {
                    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(previousScenes[i]);
                    if (unloadOp == null)
                    {
                        continue;
                    }

                    while (!unloadOp.isDone)
                    {
                        await Awaitable.NextFrameAsync(token);
                    }
                }
            }

            public async Awaitable ReleaseOnFailure()
            {
                if (_op == null)
                {
                    return;
                }

                await RunWithTimeoutAsync(CleanupAsync, _cleanupTimeoutSeconds, CancellationToken.None,
                    "실패한 Build Settings 씬 정리", treatTimeoutAsSuccess: true);
            }

            private async Awaitable CleanupAsync()
            {
                _op.allowSceneActivation = true;

                while (!_op.isDone)
                {
                    await Awaitable.NextFrameAsync(CancellationToken.None);
                }

                if (SceneManager.sceneCount == 0)
                {
                    return;
                }

                Scene scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    return;
                }

                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(scene);
                if (unloadOp == null)
                {
                    return;
                }

                while (!unloadOp.isDone)
                {
                    await Awaitable.NextFrameAsync(CancellationToken.None);
                }
            }
        }

        private sealed class AddressableSceneLoadOperation : ISceneLoadOperation
        {
            private readonly string _address;
            private readonly float _cleanupTimeoutSeconds;
            private readonly AsyncOperationHandle<SceneInstance> _handle;
            private AsyncOperation _activation;

            public AddressableSceneLoadOperation(string address, float cleanupTimeoutSeconds)
            {
                _address = address;
                _cleanupTimeoutSeconds = cleanupTimeoutSeconds;
                _handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Single, activateOnLoad: false);
            }

            public bool FailedToStart => false;

            public async Awaitable WaitUntilReadyAsync(Action<float> onProgress, CancellationToken token)
            {
                Task<SceneInstance> task = _handle.Task;

                while (!task.IsCompleted)
                {
                    onProgress(_handle.PercentComplete);
                    await Awaitable.NextFrameAsync(token);
                }

                await task;

                if (_handle.Status != AsyncOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Addressables 씬 로드에 실패했습니다: {_address} (Status={_handle.Status})",
                        _handle.OperationException);
                }

                onProgress(1f);
            }

            public void Activate() => _activation = _handle.Result.ActivateAsync();

            public async Awaitable WaitUntilActivationDoneAsync(CancellationToken token)
            {
                while (_activation != null && !_activation.isDone)
                {
                    await Awaitable.NextFrameAsync(token);
                }
            }

            public AsyncOperationHandle<SceneInstance>? Handle => _handle.IsValid() ? _handle : (AsyncOperationHandle<SceneInstance>?)null;

            public async Awaitable ReleaseOnFailure()
            {
                if (!_handle.IsValid())
                {
                    return;
                }

                await RunWithTimeoutAsync(ActivateThenReleaseAsync, _cleanupTimeoutSeconds, CancellationToken.None,
                    $"실패한 Addressables 씬(\"{_address}\") 정리", treatTimeoutAsSuccess: true);
            }

            private async Awaitable ActivateThenReleaseAsync()
            {
                if (_handle.Status == AsyncOperationStatus.Succeeded)
                {
                    AsyncOperation activation = _handle.Result.ActivateAsync();
                    if (activation != null)
                    {
                        while (!activation.isDone)
                        {
                            await Awaitable.NextFrameAsync(CancellationToken.None);
                        }
                    }
                }

                Addressables.Release(_handle);
            }
        }

        private sealed class LoadContributor
        {
            public readonly string Label;
            public readonly float Weight;
            public readonly bool Critical;
            public float Progress;
            public bool Done;
            public Exception Error;

            public LoadContributor(string label, float weight, bool critical = true)
            {
                Label = label;
                Weight = Mathf.Max(0f, weight);
                Critical = critical;
            }
        }

        private static float ComputeWeightedProgress(List<LoadContributor> contributors)
        {
            float totalWeight = 0f;
            float weightedSum = 0f;

            for (int i = 0; i < contributors.Count; i++)
            {
                LoadContributor c = contributors[i];
                totalWeight += c.Weight;
                weightedSum += c.Progress * c.Weight;
            }

            return totalWeight > 0f ? Mathf.Clamp01(weightedSum / totalWeight) : 0f;
        }

        private static bool AllDone(List<LoadContributor> contributors)
        {
            for (int i = 0; i < contributors.Count; i++)
            {
                if (!contributors[i].Done)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ThrowIfCriticalFailure(List<LoadContributor> contributors)
        {
            List<Exception> errors = null;

            for (int i = 0; i < contributors.Count; i++)
            {
                LoadContributor c = contributors[i];
                if (c.Error == null)
                {
                    continue;
                }

                if (c.Critical)
                {
                    (errors ??= new List<Exception>()).Add(c.Error);
                }
                else
                {
                    Debug.LogWarning($"[SceneLoadingManager] 선택적 단계 \"{c.Label}\" 실패(Critical=false, 계속 진행): {c.Error}");
                }
            }

            if (errors == null)
            {
                return;
            }

            if (errors.TrueForAll(e => e is OperationCanceledException))
            {
                throw errors[0];
            }

            throw errors.Count == 1 ? errors[0] : new AggregateException(errors);
        }

        private static async Awaitable RunSceneReadyContributorAsync(ISceneLoadOperation op, LoadContributor contributor,
            float timeoutSeconds, CancellationToken token)
        {
            try
            {
                await RunWithTimeoutAsync(() => op.WaitUntilReadyAsync(p => contributor.Progress = p, token),
                    timeoutSeconds, token, $"씬 오퍼레이션(\"{contributor.Label}\") 로드 대기", treatTimeoutAsSuccess: false);
                contributor.Progress = 1f;
            }
            catch (Exception e)
            {
                contributor.Error = e;
            }
            finally
            {
                contributor.Done = true;
            }
        }

        private static async Awaitable RunExtraStepContributorAsync(SceneLoadStep step, LoadContributor contributor,
            float timeoutSeconds, CancellationToken token)
        {
            try
            {
                if (step.RunAsync == null)
                {
                    throw new ArgumentException(
                        $"SceneLoadStep \"{contributor.Label}\"이 기본값(default)입니다. new SceneLoadStep(...) 생성자로 만든 값만 extraSteps에 전달하세요.");
                }

                await RunWithTimeoutAsync(() => step.RunAsync(p => contributor.Progress = Mathf.Clamp01(p), token),
                    timeoutSeconds, token, $"SceneLoadStep(\"{step.Label}\")", treatTimeoutAsSuccess: false);
                contributor.Progress = 1f;
            }
            catch (Exception e)
            {
                contributor.Error = e;
            }
            finally
            {
                contributor.Done = true;
            }
        }
    }
}
