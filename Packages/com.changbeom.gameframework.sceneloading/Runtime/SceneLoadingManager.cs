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

        /// <summary>재시도/폴백까지 전부 소진된 뒤 최종 실패했을 때만 발행됩니다. MaxRetryCount가
        /// 0(기본값)이면 시도가 1번뿐이라 기존과 동일하게 그 1번의 실패에 바로 발행됩니다.</summary>
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

        /// <summary>생성된 ESceneKey로 씬을 전환합니다.</summary>
        public async Awaitable LoadSceneAsync(ESceneKey sceneKey)
        {
            await LoadSceneAsync(sceneKey, null);
        }

        /// <summary>생성된 ESceneKey로 씬을 전환하면서, extraSteps의 진행률까지 Progress에 가중 합산합니다.</summary>
        public async Awaitable LoadSceneAsync(ESceneKey sceneKey, IReadOnlyList<SceneLoadStep> extraSteps)
        {
            if (sceneKey == ESceneKey.None)
            {
                Debug.LogError("[SceneLoadingManager] ESceneKey.None으로는 씬을 로드할 수 없습니다.");
                OnSceneLoadFailed?.Invoke(sceneKey.ToString());
                return;
            }

            await LoadSceneAsync(sceneKey.ToString(), extraSteps);
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

        // 모든 공개 LoadSceneAsync*/LoadSceneFromAddressableAsync* 오버로드가 최종적으로 여기로
        // 모입니다. IsLoading = true 자체는 예외를 던질 수 없는 단순 대입이라 try 밖에 둡니다.
        // 그 다음부터는 이벤트 발행을 포함해 전부 예외를 던질 수 있으므로(구독자가 던지는
        // 경우 포함), 반드시 try 안에 있어야 finally에서 IsLoading을 복구할 수 있습니다.
        // 재시도/폴백 전체가 이 try/finally 하나 안에서 끝나므로, 그 사이에 IsLoading이
        // 잠깐이라도 false로 풀려서 다른 LoadSceneAsync 호출이 끼어드는 일은 없습니다.
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

                // 씬 전환 전 팝업/토스트 대기열까지 정리 (UIManager.CloseAll이 이미 이 용도로 문서화되어 있음)
                UIManager.Instance.CloseAll();

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
                // 앱 종료 등으로 destroyCancellationToken이 취소된 정상적인 상황이라 에러로
                // 남기지 않습니다.
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

        // 실패할 때마다 재시도하고(최대 1+MaxRetryCount번, 폴백이면 1+FallbackMaxRetryCount번),
        // 모든 시도가 실패하면 false를 반환합니다. OperationCanceledException(앱 종료)은
        // 재시도하지 않고 그대로 위로 전파합니다.
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

        // 씬 로드 시도 한 번. 실패하면(씬을 못 찾음, 예외, Critical 단계 실패 등) 예외를 던져서
        // RunAttemptsWithRetryAsync가 재시도 여부를 판단하게 합니다.
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

            // 로드가 실제로 시작된 뒤에만(위에서 FailedToStart면 이미 예외를 던짐) 지금 씬을
            // 떠난다고 확정할 수 있으므로, 이 시점에 지금 씬의 ISceneExitPoint를 실행합니다.
            // 로딩 화면이 이미 화면을 덮은 뒤라 여기서 하는 정리 작업은 사용자에게 보이지
            // 않습니다. 재시도/폴백 전체에서 아직 원래 씬을 실제로 떠난 적이 없으므로(활성화
            // 전까지는 계속 원래 씬에 머물러 있음) state.ExitPointsRan으로 딱 1번만 실행합니다.
            if (!state.ExitPointsRan)
            {
                await RunExitPointsAsync();
                state.ExitPointsRan = true;
            }

            bool activated = false;

            try
            {
                // 씬 오퍼레이션 자신(가중치 1)과 extraSteps를 전부 동일한 취급의 "기여자"로
                // 모아 동시에 시작하고, 매 프레임 가중 평균으로 Progress를 갱신합니다.
                List<LoadContributor> contributors = new List<LoadContributor> { new LoadContributor(request.Label, 1f) };
                _ = RunSceneReadyContributorAsync(op, contributors[0], _settings.SceneOperationTimeoutSeconds, destroyCancellationToken);

                if (extraSteps != null)
                {
                    for (int i = 0; i < extraSteps.Count; i++)
                    {
                        SceneLoadStep step = extraSteps[i];
                        // default(SceneLoadStep)(RunAsync == null)은 항상 Critical로 취급해서
                        // "잘못된 값을 실수로 넘겼는데 Critical=false라 조용히 씬 로드가
                        // 성공한 것처럼 보이는" 상황을 막습니다.
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

                // 로딩 화면이 최소 이 시간만큼은 떠 있도록, 이미 지난 시간을 뺀 나머지만 기다립니다.
                // 빠른 로드에서 화면이 순간적으로 깜빡이고 사라지는 것을 방지합니다. 재시도가
                // 있었어도 StartTime은 맨 처음(로딩 화면을 처음 띄운 시점) 1번만 캡처된 값이라,
                // 재시도할 때마다 이 대기 시간이 매번 더 늘어나지 않습니다.
                float remain = _settings.MinimumLoadingScreenDuration - (Time.unscaledTime - state.StartTime);
                if (remain > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(remain, destroyCancellationToken);
                }

                // Activate() 호출 직후 activated = true로 표시합니다. 활성화 대기 중에
                // destroyCancellationToken이 취소되면(앱 종료) ReleaseOnFailure()가
                // 이미 활성화가 시작된 Addressables 핸들을 건드리지 않도록 하기 위함입니다.
                op.Activate();
                activated = true;
                await op.WaitUntilActivationDoneAsync(destroyCancellationToken);

                TrackAddressableSceneHandle(op);

                // 새 씬이 이제 활성 씬입니다. 이 씬의 ISceneEntryPoint가 전부 끝날 때까지
                // 로딩 화면을 유지해서, 플레이어 스폰/씬 전용 데이터 로드 등이 실제로
                // 끝난 뒤에야 화면이 사라지도록 합니다.
                await RunEntryPointsAsync();

                OnSceneLoadCompleted?.Invoke(request.Label);
            }
            finally
            {
                if (!activated)
                {
                    // 여기서 반드시 완료를 기다려야 합니다 - 그냥 fire-and-forget으로 던져두면
                    // (예: 폴백이 곧바로 다음 시도를 시작해) 이 정리 작업과 다음 시도의
                    // BuildSettingsSceneLoadOperation이 동시에 씬 목록을 건드리게 되고,
                    // 그러면 "방금 로드된 씬"을 찾는 로직이 서로 다른 오퍼레이션의 씬을
                    // 잘못 집을 수 있는 경쟁 상태가 생깁니다. await로 완전히 끝난 뒤에만
                    // 다음 시도가 시작되도록 해서 이 경쟁 상태 자체가 생기지 않게 합니다.
                    // try/catch로 감싸는 이유: 여기서 예외가 나면 finally 블록의 특성상
                    // 원래 실패 원인(Critical 단계 실패 등)이 이 예외로 덮여버려 재시도
                    // 루프의 로그가 진짜 원인을 알 수 없게 되기 때문입니다.
                    try
                    {
                        await op.ReleaseOnFailure();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SceneLoadingManager] 실패한 로드를 정리하지 못했습니다: {e}");
                    }

                    // 활성화까지 못 갔으니 실제 씬은 바뀌지 않았습니다(정리까지 끝난
                    // 지금은 원래 씬으로 완전히 복원된 상태). op.FailedToStart 경로(261-266줄)는
                    // 이 finally에 오기 전에 이미 따로 복원하지만, Critical 단계 실패 등
                    // 나머지 실패 경로는 전부 여기서 한 번에 복원합니다.
                    CurrentSceneName = SceneManager.GetActiveScene().name;
                }
            }
        }

        private void TrackAddressableSceneHandle(ISceneLoadOperation op)
        {
            // LoadSceneMode.Single이어도 이전에 Addressables로 로드했던 씬의 핸들은 자동
            // 해제되지 않으므로(Unity가 GameObject는 정리해도 Addressables 참조 카운트는
            // 그대로 남김), 새 씬이 활성화될 때마다 직전에 추적하던 핸들을 언로드합니다.
            // 새 씬이 Build Settings 경로였어도 이전 핸들은 그대로 정리해야 합니다.
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

        // 사용자 코드(ISceneEntryPoint/ISceneExitPoint 훅, SceneLoadStep)가 반환한 Awaitable이
        // 예외 없이 그냥 영원히 끝나지 않으면, try/finally만으로는 IsLoading을 되돌릴 방법이
        // 없습니다(finally는 예외 또는 정상 반환에서만 실행됨). 그래서 타임아웃과 경합시켜,
        // 타임아웃이 먼저 끝나면 더 기다리지 않고 다음 단계로 넘어갑니다 - action 자체는
        // 백그라운드에서 계속 실행되도록 두고(취소할 방법이 없으므로), 그 결과는 더 이상
        // 씬 전환 파이프라인을 막지 않게만 합니다.
        //
        // treatTimeoutAsSuccess로 타임아웃을 어떻게 다룰지 호출부가 결정합니다:
        // - true (ISceneEntryPoint/ExitPoint 훅): 타임아웃 = 성공한 걸로 치고 넘어감. 이
        //   훅들은 "정리/초기화 작업"이라 못 끝나도 씬 전환 자체를 막을 이유가 없습니다.
        // - false (SceneLoadStep): 타임아웃 = TimeoutException으로 실패 처리. SceneLoadStep은
        //   이미 Critical 플래그로 "이 단계가 실패하면 씬 로드 전체를 실패로 볼지"를 표현하고
        //   있으므로, 여기서 "타임아웃 = 그냥 성공"으로 처리해버리면 Critical=true인 단계가
        //   실제로는 못 끝났는데도 씬이 정상 로드된 것처럼 보이는 잘못된 결과가 됩니다.
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

        // 재시도/폴백 전체에 걸쳐 공유해야 하는 상태입니다. StartTime은 최소 노출시간 계산
        // 기준점이라 로딩 화면을 맨 처음 띄운 시점 1번만 캡처해야 하고, ExitPointsRan은
        // 원래 씬을 아직 실제로 떠나지 않은 한(활성화 전까지는 계속 원래 씬에 있음) 재시도마다
        // 중복 실행되면 안 됩니다.
        private sealed class LoadPipelineState
        {
            public float StartTime;
            public bool ExitPointsRan;
        }

        // 씬을 Build Settings 경로로 로드할지 Addressables 경로로 로드할지를 감추는 요청
        // 값 객체입니다. Begin()이 알맞은 ISceneLoadOperation을 만듭니다.
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
                ? new AddressableSceneLoadOperation(Label)
                : (ISceneLoadOperation)new BuildSettingsSceneLoadOperation(Label, cleanupTimeoutSeconds);
        }

        // "씬 오퍼레이션을 어떻게 시작·진행·활성화하는지"만 추상화합니다. 로딩 화면 표시,
        // 최소 노출시간, ISceneEntryPoint/ExitPoint, IsLoading 관리 등 나머지 파이프라인은
        // Build Settings 경로와 Addressables 경로가 완전히 동일한 코드를 공유합니다.
        private interface ISceneLoadOperation
        {
            /// <summary>시작 자체가 동기적으로 실패했는지 (Build Settings: 미등록 씬). Addressables는
            /// 항상 false이고, 실패는 WaitUntilReadyAsync에서 비동기적으로만 드러납니다.</summary>
            bool FailedToStart { get; }

            /// <summary>활성화 준비가 될 때까지 대기하며 onProgress(0~1)로 진행률을 보고합니다. 실패 시 예외를 던집니다.</summary>
            Awaitable WaitUntilReadyAsync(Action<float> onProgress, CancellationToken token);

            void Activate();
            Awaitable WaitUntilActivationDoneAsync(CancellationToken token);

            /// <summary>이 시도가 실패해서 버려질 때 리소스를 정리합니다 (Addressables 핸들 해제,
            /// Build Settings 경로의 경우 어차피 끝까지 로드된 씬을 언로드하는 것 등). 호출부는
            /// 이 정리가 완전히 끝난 뒤에야 다음 시도를 시작하므로, 반드시 실제로 완료될 때까지
            /// 기다리는 Awaitable을 반환해야 합니다 - fire-and-forget으로 구현하면 다음 시도의
            /// 씬 오퍼레이션과 동시에 진행되면서 서로의 씬을 혼동할 수 있는 경쟁 상태가 생깁니다.</summary>
            Awaitable ReleaseOnFailure();
        }

        private sealed class BuildSettingsSceneLoadOperation : ISceneLoadOperation
        {
            private readonly AsyncOperation _op;
            private readonly float _cleanupTimeoutSeconds;

            // LoadSceneMode.Single + allowSceneActivation=false는 한 번 시작하면 취소할
            // 방법이 없습니다 - 활성화하거나, 영원히 "로딩 중"으로 방치하거나 둘 중
            // 하나뿐입니다(Unity에 씬 로드를 중간에 멈추는 API 자체가 없음). Critical
            // SceneLoadStep 실패 등으로 이 로드를 포기해야 할 때, 씬이 Hierarchy에
            // 영원히 "(is loading)"로 남아 재시도마다 하나씩 쌓이는 문제가 있었습니다.
            // 그래서 내부적으로는 항상 Additive로 로드해두고, 성공하면
            // WaitUntilActivationDoneAsync에서 이전 씬들을 직접 언로드하고 새 씬을 활성
            // 씬으로 지정해 Single 모드처럼 보이게 하고, 실패하면 ReleaseOnFailure에서
            // 이 씬만 깨끗하게 언로드합니다. Additive는 성공적으로 로드되어도(=활성화되어도)
            // 다른 씬에 영향을 주지 않으므로, 실패 경로에서도 안전하게 끝까지 로드시킨 뒤
            // 바로 지울 수 있습니다.
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

                // 같은 이름의 씬을 자기 자신 위에 다시 로드하는 경우(자기 자신 리로드)
                // GetSceneByName(sceneName)은 기존 인스턴스와 새 인스턴스 중 어느 쪽인지
                // 구분하지 못합니다. Unity는 Additive로 새로 로드된 씬을 항상 씬 목록
                // 맨 끝에 추가하므로, isDone이 true가 된 직후(중간에 다른 await 없이)
                // 바로 마지막 인덱스를 읽으면 이 오퍼레이션이 방금 추가한 씬을 정확히
                // 집을 수 있습니다.
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

                // DontDestroyOnLoad에 있는 오브젝트들은 SceneManager.GetSceneAt에 아예
                // 잡히지 않는 별도의 씬이라, 이 목록에 포함되지 않고 자동으로 보존됩니다.
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

                // 호출부(RunSingleAttemptAsync)가 이 메서드를 반드시 await하고 나서야 다음
                // 시도를 시작하므로, 정리 도중 "마지막 인덱스"로 씬을 찾는 게 안전합니다 -
                // 이 메서드가 끝나기 전까지는 다른 BuildSettingsSceneLoadOperation이 동시에
                // 씬 목록에 씬을 추가할 수 없습니다. 다만 이 정리 자체가(예: 씬의 Awake에서
                // 무거운 동기 작업 등으로) 끝나지 않으면 재시도/폴백 파이프라인 전체가
                // 멈춰버리므로, 다른 훅들과 동일하게 타임아웃으로 보호합니다 - 타임아웃이
                // 나면 정리를 더 기다리지 않고 넘어갑니다(씬 하나가 남을 수 있지만, 파이프라인
                // 전체가 멈추는 것보다는 낫습니다).
                await RunWithTimeoutAsync(CleanupAsync, _cleanupTimeoutSeconds, CancellationToken.None,
                    "실패한 Build Settings 씬 정리", treatTimeoutAsSuccess: true);
            }

            private async Awaitable CleanupAsync()
            {
                // 이미 시작된 로드는 중간에 멈출 수 없으므로, 끝까지 로드되게 둔 뒤(Additive라
                // 다른 씬에는 영향 없음) 곧바로 언로드해서 정리합니다.
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
            private readonly AsyncOperationHandle<SceneInstance> _handle;
            private AsyncOperation _activation;

            public AddressableSceneLoadOperation(string address)
            {
                _address = address;
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

            public Awaitable ReleaseOnFailure()
            {
                if (_handle.IsValid())
                {
                    Addressables.Release(_handle);
                }

                // Addressables.Release는 동기 호출이라 기다릴 게 없지만, 인터페이스
                // 시그니처를 맞추기 위해 이미 완료된 Awaitable을 반환합니다.
                AwaitableCompletionSource tcs = new AwaitableCompletionSource();
                tcs.SetResult();
                return tcs.Awaitable;
            }
        }

        // 씬 오퍼레이션 자신과 각 SceneLoadStep을 동일하게 취급하는 진행률 추적 단위입니다.
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

            // 앱 종료 등으로 destroyCancellationToken이 취소되면 여러 contributor가 같은
            // 프레임에 동시에 OperationCanceledException을 받을 수 있습니다. 이걸
            // AggregateException으로 감싸버리면 RunAttemptsWithRetryAsync의
            // catch (OperationCanceledException) { throw; }가 못 잡아서 취소가 일반
            // 실패로 오분류되고(재시도/폴백이 도는 등) 앱 종료 중에 불필요한 동작이
            // 일어날 수 있으므로, 전부 취소였다면 그대로 하나만 던집니다.
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
                // Build Settings에서 비활성화된 씬 이름으로 로드하면 op가 null이 아닌
                // 진행률이 절대 안 올라가는 AsyncOperation을 반환할 수 있고, Addressables도
                // 원격 다운로드가 멈추면 마찬가지로 영원히 끝나지 않을 수 있습니다. SceneLoadStep과
                // 동일하게(성공 처리가 아니라 실패로) 타임아웃 보호합니다 - 씬 오퍼레이션은
                // "일단 못 끝나면 건너뛰고 계속 진행"할 수 있는 대상이 아니라 항상 필수이므로.
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
                // default(SceneLoadStep)처럼 생성자를 거치지 않은 값이 실수로 넘어오면
                // RunAsync가 null이라 여기서 바로 걸러냅니다 (LoadContributor를 만들 때
                // 이 경우는 항상 Critical로 취급하도록 이미 처리되어 있습니다).
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
