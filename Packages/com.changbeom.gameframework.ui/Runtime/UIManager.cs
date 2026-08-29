using System;
using System.Collections.Generic;
using System.Threading;
using GameFramework.Core;
using GameFramework.Pooling;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.UISystem
{
    [BootPriority(-10)]
    public class UIManager : MonoSingleton<UIManager>
    {
        private UIManagerSettings _settings;

        private Canvas _uiRootCanvas;
        private RectTransform _hudRoot;
        private RectTransform _popupRoot;
        private RectTransform _toastRoot;
        private RectTransform _overlayRoot;
        private GameObject _modalBlocker;

        private GameObject _hudInstance;
        private GameObject _overlayInstance;

        private readonly List<PopupRequest> _pending = new();
        private readonly HashSet<UIToastBase> _activeToasts = new();

        /// <summary>EPopupPolicy.ReplaceCurrent로 예약된, 우선순위 정렬과 무관하게 다음
        /// 차례에 반드시 열려야 하는 요청입니다. _pending과 별도로 관리합니다.</summary>
        private PopupRequest _forcedNext;

        private UIPopupBase _current;
        private Action<object> _currentResultCallback;
        private bool _isClosingCurrent;

        private int _sequenceCounter;
        private bool _processScheduled;

        /// <summary>팝업보다 아래에 있는, 상시 표시되는 HUD 요소용 지속 레이어입니다. 여기에 자신의 HUD 프리팹을 부모로 연결하세요.</summary>
        public Transform HudRoot => _hudRoot;

        /// <summary>팝업과 토스트보다 위에 있는 최상단 레이어입니다. 전체화면 로딩/전환 연출용입니다.</summary>
        public Transform OverlayRoot => _overlayRoot;

        /// <summary>UIManagerSettings.HudPrefabOverride로 미리 생성해둔 인스턴스입니다(설정 안 했으면 null). 시작 시 비활성화 상태이므로 필요할 때 SetActive(true)로 켜세요.</summary>
        public GameObject HudInstance => _hudInstance;

        /// <summary>UIManagerSettings.OverlayPrefabOverride로 미리 생성해둔 인스턴스입니다(설정 안 했으면 null). 시작 시 비활성화 상태이므로 필요할 때 SetActive(true)로 켜세요.</summary>
        public GameObject OverlayInstance => _overlayInstance;

        public bool IsAnyPopupOpen => _current != null;
        public bool HasPendingPopups => _pending.Count > 0;
        public bool IsBlockingInput => _current != null;

        /// <summary>현재 열려 있는 팝업이 뒤로가기/취소 입력으로 닫혀도 되는지 여부입니다. 팝업이 없으면 false입니다.</summary>
        public bool CurrentPopupCloseableByBackButton => _current != null && _current.CloseableByBackButton;

        protected override void OnInitialize()
        {
            DontDestroyOnLoad(gameObject);

            _settings = LoadSettings();

            EnsureCanvasRoot();
            EnsureLayers();
            EnsureModalBlocker();
            EnsurePersistentLayerInstances();
        }

        private static UIManagerSettings LoadSettings()
        {
            UIManagerSettings settings = Resources.Load<UIManagerSettings>(UIManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[UIManager] Resources/{UIManagerSettings.ResourcePath}에서 UIManagerSettings 에셋을 찾지 못했습니다. 기본값을 사용합니다. Assets/Create/Game Framework/UI System/UI Manager Settings로 에셋을 만드세요.");
            return ScriptableObject.CreateInstance<UIManagerSettings>();
        }

        private void Update()
        {
            if (_settings.EnableBackButtonClose == false)
            {
                return;
            }

            if (_current == null || _current.CloseableByBackButton == false)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) == true)
            {
                CloseTopPopup();
            }
        }

        private void LateUpdate()
        {
            if (_processScheduled == false)
            {
                return;
            }

            _processScheduled = false;
            ProcessPending();
        }

        // ---- 팝업 ----

        public void RequestPopup(
            UIPopupBase prefab,
            EPopupPriority priority,
            object payload = null,
            bool unique = true,
            EPopupPolicy policy = EPopupPolicy.PreemptIfHigher,
            Action<object> onResult = null)
        {
            if (prefab == null)
            {
                return;
            }

            Type t = prefab.GetType();

            if (unique == true)
            {
                if (IsAlreadyForcedNextType(t) == true)
                {
                    return;
                }

                if (IsAlreadyQueuedType(t) == true)
                {
                    return;
                }

                if (IsAlreadyOpenType(t) == true)
                {
                    return;
                }
            }

            PopupRequest req = new PopupRequest();
            req.prefab = prefab;
            req.instance = null;
            req.priority = priority;
            req.payload = payload;
            req.onResult = onResult;
            req.sequence = _sequenceCounter;
            _sequenceCounter++;

            // _isClosingCurrent인 동안(닫기 애니메이션이 아직 진행 중인 동안)에는 _current를
            // 건드리지 않습니다. 여기서 SuspendCurrentToPending/ClosePopup을 또 호출하면,
            // 같은 인스턴스가 "닫히는 중"이면서 동시에 "_pending에서 재개 가능"한 모순된
            // 상태가 되어, 닫기 완료 콜백이 풀에 반환한 인스턴스를 ProcessPending이 다시
            // 꺼내 쓰려고 하는 이중 사용으로 이어질 수 있습니다. 이 창에서는 그냥 대기열에
            // 넣어두면, 닫기가 끝나는 순간 _processScheduled로 자연스럽게 처리됩니다.
            if (_current != null && _isClosingCurrent == false)
            {
                if (policy == EPopupPolicy.PreemptIfHigher &&
                    priority > (EPopupPriority)_current.OpenPriority)
                {
                    // req를 바로 OpenRequestNow로 열어버리면, 이미 _pending에 req보다도
                    // 더 우선순위가 높은 요청이 대기 중이더라도 무시하고 req가 먼저
                    // 열려버립니다. 대신 대기열에 넣고 ProcessPending이 정렬된 순서로
                    // 고르게 합니다.
                    SuspendCurrentToPending();
                    _pending.Add(req);
                    _processScheduled = true;
                    return;
                }

                if (policy == EPopupPolicy.ReplaceCurrent)
                {
                    ClosePopup(_current);

                    // ProcessPending은 항상 SortPending으로 우선순위 정렬을 하기 때문에,
                    // _pending.Insert(0, ...)로 앞쪽에 끼워 넣어도 정렬 후에는 위치가
                    // 의미 없어집니다(우선순위가 낮으면 다른 대기 요청에 밀림) - "교체"라는
                    // 이름과 다르게 동작했습니다. 정렬 대상이 아닌 별도의 "다음에 반드시
                    // 열릴 요청" 슬롯에 넣어서, 우선순위와 무관하게 다음 차례에 확실히
                    // 열리도록 합니다.
                    if (_forcedNext != null)
                    {
                        // 아직 처리 안 된 이전 ReplaceCurrent 요청이 있다면 덮어씁니다.
                        // onResult를 그냥 버리면 결과를 기다리던 호출부가 영원히 안
                        // 풀릴 수 있으니 null로라도 반드시 한 번 불러줍니다.
                        _forcedNext.onResult?.Invoke(null);
                    }

                    _forcedNext = req;
                    return;
                }
            }

            _pending.Add(req);
            _processScheduled = true;
        }

        /// <summary>결과를 반환하는 팝업(예: 확인/취소 다이얼로그)을 위한 타입 지정 편의 래퍼입니다.</summary>
        public void RequestPopup<TResult>(
            UIPopupBase prefab,
            EPopupPriority priority,
            Action<TResult> onResult,
            object payload = null,
            bool unique = true,
            EPopupPolicy policy = EPopupPolicy.PreemptIfHigher)
        {
            RequestPopup(prefab, priority, payload, unique, policy,
                result => onResult?.Invoke(result is TResult typed ? typed : default));
        }

        /// <summary>Pool Settings에 등록된 Key로 팝업 프리팹을 찾아 요청합니다. 프리팹 참조를 직접 들고 있을 필요가 없습니다.</summary>
        public void RequestPopup(
            string key,
            EPopupPriority priority,
            object payload = null,
            bool unique = true,
            EPopupPolicy policy = EPopupPolicy.PreemptIfHigher,
            Action<object> onResult = null)
        {
            UIPopupBase prefab = ResolvePopupPrefab(key);

            if (prefab == null)
            {
                return;
            }

            RequestPopup(prefab, priority, payload, unique, policy, onResult);
        }

        /// <summary>Key 기반 RequestPopup의 결과 콜백(<typeparamref name="TResult"/>) 버전입니다.</summary>
        public void RequestPopup<TResult>(
            string key,
            EPopupPriority priority,
            Action<TResult> onResult,
            object payload = null,
            bool unique = true,
            EPopupPolicy policy = EPopupPolicy.PreemptIfHigher)
        {
            UIPopupBase prefab = ResolvePopupPrefab(key);

            if (prefab == null)
            {
                return;
            }

            RequestPopup(prefab, priority, onResult, payload, unique, policy);
        }

        private UIPopupBase ResolvePopupPrefab(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[UIManager] 빈 key로는 팝업을 열 수 없습니다.");
                return null;
            }

            // 앱 종료 중에는 매니저 종료 순서가 보장되지 않아 PoolManager.Instance가
            // 이미 null일 수 있습니다.
            if (PoolManager.Instance == null)
            {
                return null;
            }

            if (!PoolManager.Instance.TryGetPrefab(key, out GameObject prefabGo))
            {
                Debug.LogError($"[UIManager] PoolSettings에 등록되지 않은 key입니다: {key}");
                return null;
            }

            if (!prefabGo.TryGetComponent(out UIPopupBase prefab))
            {
                Debug.LogError($"[UIManager] \"{key}\" 프리팹에 UIPopupBase 컴포넌트가 없습니다.");
                return null;
            }

            return prefab;
        }

        public void CloseTopPopup(object result = null)
        {
            if (_current == null)
            {
                return;
            }

            ClosePopup(_current, result);
        }

        public void ClosePopup(UIPopupBase target)
        {
            ClosePopup(target, null);
        }

        /// <summary><paramref name="result"/>은 이 팝업의 RequestPopup 호출 시 전달된 onResult 콜백으로 전달됩니다.</summary>
        public void ClosePopup(UIPopupBase target, object result)
        {
            // _isClosingCurrent를 확인하지 않으면, 닫기 애니메이션이 끝나기 전에
            // ClosePopup이 한 번 더 호출됐을 때 target.RequestClose가 두 번 걸리면서
            // 두 번째 호출의 onResult(이미 null로 비워진 상태)가 첫 번째 호출의
            // onResult를 덮어써 원래 콜백이 영영 호출되지 않는 문제가 있었습니다.
            if (_current != target || _isClosingCurrent)
            {
                return;
            }

            _isClosingCurrent = true;

            Action<object> onResult = _currentResultCallback;
            _currentResultCallback = null;

            target.RequestClose(() =>
            {
                // 닫기 애니메이션이 끝나기 전까지는 모달 블로커를 켜둔 채로 둡니다 -
                // 미리 꺼버리면 팝업이 화면에서 아직 사라지는 중인데도 그 뒤의 UI가
                // 클릭을 받아버리는 입력 누수가 있었습니다.
                _modalBlocker.SetActive(false);

                // 앱 종료 중에는 매니저 종료 순서가 보장되지 않아 PoolManager.Instance가
                // 이미 null일 수 있습니다.
                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Despawn(target.gameObject);
                }

                // _current가 그 사이 다른 팝업으로 바뀌어 있다면(정상 흐름에서는 위의
                // RequestPopup 가드 때문에 일어나지 않아야 하지만) 덮어쓰지 않습니다.
                if (_current == target)
                {
                    _current = null;
                }

                _isClosingCurrent = false;
                _processScheduled = true;
                onResult?.Invoke(result);
            });
        }

        /// <summary>대기열을 즉시 비우고 현재 팝업을 닫습니다 (결과값 전달 없음). 씬 전환 시 정리용입니다.</summary>
        public void CloseAll()
        {
            // 대기열에는 선점(Preempt)으로 인해 정지된 채 인스턴스를 들고 있는 요청이
            // 섞여 있을 수 있습니다. 그냥 Clear만 하면 그 인스턴스가 풀로 반환되지 않고
            // 비활성 상태로 영구히 남아 풀의 maxCount를 잠식하므로, 반드시 Despawn한 뒤 비웁니다.
            for (int i = 0; i < _pending.Count; i++)
            {
                UIPopupBase suspended = _pending[i].instance;
                if (suspended != null && PoolManager.Instance != null)
                {
                    PoolManager.Instance.Despawn(suspended.gameObject);
                }
            }

            _pending.Clear();

            // 토스트는 비모달이라 팝업과 독립적으로 여러 개 동시에 떠 있을 수 있습니다.
            // 여기서 비워주지 않으면 씬 전환 등으로 CloseAll이 호출돼도 이미 떠 있던
            // 토스트 인스턴스가 디스폰되지 않고 남거나, _activeToasts에 계속 남아있는
            // 항목 때문에 나중에 파괴된 인스턴스를 대상으로 HideToast/자동 숨김 타이머가
            // 동작하려 드는 누수로 이어집니다. 숨김 연출을 기다리지 않고 즉시 디스폰합니다.
            if (_activeToasts.Count > 0)
            {
                foreach (UIToastBase toast in _activeToasts)
                {
                    if (toast != null && PoolManager.Instance != null)
                    {
                        PoolManager.Instance.Despawn(toast.gameObject);
                    }
                }

                _activeToasts.Clear();
            }

            // _pending과 마찬가지로 아직 처리 안 된 ReplaceCurrent 예약도 정리 대상입니다.
            // onResult를 그냥 버리면 결과를 기다리던 호출부가 영원히 안 풀릴 수 있으니
            // null로라도 반드시 한 번 불러줍니다.
            if (_forcedNext != null)
            {
                PopupRequest forced = _forcedNext;
                _forcedNext = null;
                forced.onResult?.Invoke(null);
            }

            if (_current == null || _isClosingCurrent)
            {
                return;
            }

            // ClosePopup과 마찬가지로 _current는 RequestClose의 완료 콜백에서만 비웁니다.
            // 여기서 미리 null로 만들면, 애니메이션이 끝나기 전 그 사이에 들어온
            // RequestPopup이 "열려 있는 팝업이 없다"고 착각해 새 팝업을 바로 열어버려서
            // 옛 팝업이 화면에서 채 사라지기도 전에 두 팝업이 동시에 보이는 경쟁 상태가 있었습니다.
            _isClosingCurrent = true;
            UIPopupBase target = _current;
            _currentResultCallback = null;

            target.RequestClose(() =>
            {
                _modalBlocker.SetActive(false);

                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Despawn(target.gameObject);
                }

                if (_current == target)
                {
                    _current = null;
                }

                _isClosingCurrent = false;
                _processScheduled = true;
            });
        }

        private void ProcessPending()
        {
            if (_current != null)
            {
                return;
            }

            if (_forcedNext != null)
            {
                PopupRequest forced = _forcedNext;
                _forcedNext = null;
                OpenRequestNow(forced);
                return;
            }

            if (_pending.Count <= 0)
            {
                return;
            }

            SortPending();

            PopupRequest req = _pending[0];
            _pending.RemoveAt(0);

            OpenRequestNow(req);
        }

        private void SuspendCurrentToPending()
        {
            UIPopupBase cur = _current;

            PopupRequest r = new PopupRequest();
            r.instance = cur;
            r.prefab = null;
            r.priority = (EPopupPriority)cur.OpenPriority;
            r.payload = cur.CachedPayload;
            r.sequence = cur.OpenSequence;
            r.onResult = _currentResultCallback;

            cur.OnSuspend();
            _current = null;
            _currentResultCallback = null;

            _pending.Add(r);
            SortPending();
        }

        private void OpenRequestNow(PopupRequest req)
        {
            UIPopupBase instance;

            if (req.instance != null)
            {
                instance = req.instance;
                AttachToRoot(instance.transform);

                instance.InitializePopupMeta(
                    instance.GetType(),
                    (int)req.priority,
                    req.sequence
                );

                _currentResultCallback = req.onResult;

                // OnResume 안에서 재진입 호출(예: CloseAll, RequestPopup)이 있을 경우
                // _current가 이미 이 인스턴스를 가리키고 있어야 그 호출들이 정확한
                // 상태를 보고 판단합니다 - 그래서 OnResume을 부르기 전에 대입합니다.
                _current = instance;
                instance.OnResume(req.payload);
            }
            else
            {
                // 앱 종료 중에는 매니저 종료 순서가 보장되지 않아 PoolManager.Instance가
                // 이미 null일 수 있습니다. 스폰 실패와 동일하게 취급해 요청을 흘려보냅니다.
                if (PoolManager.Instance == null)
                {
                    req.onResult?.Invoke(null);
                    return;
                }

                instance = PoolManager.Instance.Spawn(req.prefab, Vector3.zero, Quaternion.identity, _popupRoot);
                if (instance == null)
                {
                    // 스폰 실패(풀이 가득 찼을 가능성)로 이 요청은 버리지만, 대기열에 남은
                    // 다른 요청까지 같이 멈추면 안 되므로 다음 LateUpdate에 처리를 재예약합니다.
                    // onResult를 그냥 버리면 결과를 기다리던 호출부가 영원히 콜백을 못 받고
                    // 멈출 수 있으므로, null로라도 반드시 한 번 호출해서 풀어줍니다.
                    Debug.LogWarning($"[UIManager] {req.prefab.GetType().Name} 팝업을 스폰하지 못했습니다 (풀이 가득 찼을 수 있습니다). 이 요청은 건너뜁니다.");
                    req.onResult?.Invoke(null);
                    _processScheduled = true;
                    return;
                }

                AttachToRoot(instance.transform);

                instance.InitializePopupMeta(
                    req.prefab.GetType(),
                    (int)req.priority,
                    req.sequence
                );

                _currentResultCallback = req.onResult;

                // 위와 동일한 이유로 OnOpen 호출 전에 _current를 먼저 대입합니다.
                _current = instance;
                instance.OnOpen(req.payload);
            }

            _modalBlocker.SetActive(true);
            _modalBlocker.transform.SetAsLastSibling();
            instance.transform.SetAsLastSibling();
        }

        // ---- 토스트 ----

        /// <summary>비모달이며 자동으로 사라지는 토스트를 표시합니다. 여러 토스트가 동시에 표시될 수 있고 입력을 절대 막지 않습니다.</summary>
        public void ShowToast(UIToastBase prefab, object payload = null, float duration = -1f)
        {
            if (prefab == null)
            {
                return;
            }

            // 앱 종료 중에는 매니저 종료 순서가 보장되지 않아 PoolManager.Instance가
            // 이미 null일 수 있습니다.
            if (PoolManager.Instance == null)
            {
                return;
            }

            UIToastBase instance = PoolManager.Instance.Spawn(prefab, Vector3.zero, Quaternion.identity, _toastRoot);
            if (instance == null)
            {
                return;
            }

            RectTransform rt = instance.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            instance.transform.SetAsLastSibling();
            instance.Show(payload);

            _activeToasts.Add(instance);

            float d = duration >= 0f ? duration : _settings.DefaultToastDuration;
            _ = AutoHideToastAfterDelay(instance, d, instance.DespawnToken);
        }

        /// <summary>토스트를 조기에 닫습니다. 이미 자동으로 사라진 뒤에 호출해도 안전합니다.</summary>
        public void HideToast(UIToastBase toast)
        {
            if (toast == null)
            {
                return;
            }

            if (_activeToasts.Remove(toast) == false)
            {
                return;
            }

            toast.RequestHide(() =>
            {
                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Despawn(toast.gameObject);
                }
            });
        }

        private async Awaitable AutoHideToastAfterDelay(UIToastBase toast, float delay, CancellationToken despawnToken)
        {
            // despawnToken은 이 toast 인스턴스가 (수동으로 먼저 숨겨지는 등의 이유로) 풀에
            // 반환되는 순간 취소됩니다. 취소 없이 그냥 두면, 이 타이머가 나중에 발동했을 때
            // 이 인스턴스가 이미 다른 토스트로 재활용된 뒤일 수 있어 방금 새로 표시된
            // 토스트를 엉뚱하게 조기 숨김 처리해버릴 수 있습니다.
            try
            {
                await Awaitable.WaitForSecondsAsync(Mathf.Max(0.01f, delay), despawnToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            HideToast(toast);
        }

        // ---- 레이아웃 ----

        private void EnsureCanvasRoot()
        {
            if (_uiRootCanvas == null)
            {
                _uiRootCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();

                if (_uiRootCanvas != null)
                {
                    // 씬에 이미 배치돼 있던 Canvas를 그대로 재사용하는 경로입니다. 아래의
                    // "새로 생성" 경로는 go.transform.SetParent(transform, ...)로 UIManager
                    // 자신(DontDestroyOnLoad 적용됨) 밑에 붙어 자동으로 영속되지만, 이렇게
                    // 주워온 Canvas는 원래 자기가 배치된 씬에 그대로 속해 있어 영속 대상이
                    // 아닙니다. HudRoot/PopupRoot/ToastRoot/OverlayRoot가 전부 이 Canvas
                    // 밑에 생성되므로, 손대지 않으면 이 Canvas가 배치된 씬이 언로드될 때
                    // UIManager의 UI 레이어 전체가 함께 파괴됩니다. DontDestroyOnLoad는
                    // 루트 GameObject에만 적용되므로 transform.root 기준으로 호출합니다.
                    UnityEngine.Object.DontDestroyOnLoad(_uiRootCanvas.transform.root.gameObject);
                }
            }

            if (_uiRootCanvas == null)
            {
                GameObject go = new GameObject("[CanvasRoot]");
                go.layer = LayerMask.NameToLayer("UI");
                go.transform.SetParent(transform, false);

                _uiRootCanvas = go.AddComponent<Canvas>();
                _uiRootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = _settings.ReferenceResolution;
                scaler.matchWidthOrHeight = _settings.MatchWidthOrHeight;

                go.AddComponent<GraphicRaycaster>();
            }

            // 이 Canvas를 씬에서 주워왔든 새로 만들었든, 부팅 순서에 따라 프로젝트가 별도로
            // 둔 다른 Canvas(예: 게임 화면을 담은 메인 Canvas)와 Sort Order가 동률이 될 수
            // 있습니다 - 동률일 때 어느 Canvas가 위에 그려지는지는 보장되지 않아서, 팝업이
            // 다른 화면 뒤에 가려지는 문제로 이어집니다. HUD/팝업/토스트/오버레이는 항상
            // 게임의 다른 모든 UI보다 위에 있어야 하므로, 매번 명시적으로 설정합니다.
            _uiRootCanvas.sortingOrder = _settings.CanvasSortOrder;
        }

        private void EnsureLayers()
        {
            _hudRoot = CreateLayerRoot("[HudRoot]");
            _popupRoot = CreateLayerRoot("[PopupRoot]");
            _toastRoot = CreateLayerRoot("[ToastRoot]");
            _overlayRoot = CreateLayerRoot("[OverlayRoot]");
        }

        private RectTransform CreateLayerRoot(string name)
        {
            GameObject go = new GameObject(name);
            go.layer = LayerMask.NameToLayer("UI");

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(_uiRootCanvas.transform, false);

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return rt;
        }

        // HUD/Overlay는 프로젝트가 그때그때 필요할 때 Instantiate하기보다, 초기화 시점에
        // 미리 만들어두고 활성/비활성만 토글하는 편이 낫습니다(반복 Instantiate/Destroy
        // 비용, 그 사이 참조 유실 방지). 시작 시 비활성화해두는 이유는 이 인스턴스들이
        // 무엇을 보여줄지(HUD 수치, 오버레이 내용)는 UIManager가 알 수 없는 프로젝트
        // 고유 로직이라, 언제 보여줄지도 프로젝트가 직접 결정해야 하기 때문입니다.
        private void EnsurePersistentLayerInstances()
        {
            _hudInstance = InstantiateInactive(_settings.HudPrefabOverride, _hudRoot);
            _overlayInstance = InstantiateInactive(_settings.OverlayPrefabOverride, _overlayRoot);
        }

        private static GameObject InstantiateInactive(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, parent);
            instance.SetActive(false);
            return instance;
        }

        private void EnsureModalBlocker()
        {
            GameObject go = new GameObject("[ModalBlocker]");
            go.transform.SetParent(_popupRoot, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.color = _settings.ModalBlockerColor;
            img.raycastTarget = true;

            _modalBlocker = go;
            _modalBlocker.SetActive(false);
        }

        private void AttachToRoot(Transform t)
        {
            RectTransform rt = t as RectTransform;
            if (rt != null)
            {
                rt.SetParent(_popupRoot, false);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                return;
            }

            t.SetParent(_popupRoot, false);
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one;
        }

        private void SortPending()
        {
            _pending.Sort((a, b) =>
            {
                if (a.priority != b.priority)
                {
                    return b.priority.CompareTo(a.priority);
                }

                return a.sequence.CompareTo(b.sequence);
            });
        }

        // _forcedNext는 아직 애니메이션 대기 중일 뿐 _pending에 들어있지 않으므로,
        // 이 체크가 없으면 unique=true 요청도 여기 걸리지 않고 통과해 같은 타입이
        // 중복으로 열리게 됩니다.
        private bool IsAlreadyForcedNextType(Type t)
        {
            return _forcedNext != null && _forcedNext.PopupType == t;
        }

        private bool IsAlreadyQueuedType(Type t)
        {
            foreach (PopupRequest r in _pending)
            {
                if (r.PopupType == t)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAlreadyOpenType(Type t)
        {
            if (_current == null)
            {
                return false;
            }

            return _current.PopupType == t;
        }
    }
}
