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

        // EPopupPolicy.Immediate로 열린 팝업들의 스택입니다. 맨 뒤(마지막 요소)가 항상
        // 화면에서 가장 위입니다.
        private sealed class ImmediatePopupEntry
        {
            public UIPopupBase instance;
            public Action<object> onResult;
            public bool isClosing;
        }

        private readonly List<ImmediatePopupEntry> _immediateStack = new();

        /// <summary>지금 화면에서 가장 위에 있는 팝업입니다 - 즉시표시 팝업이 하나라도 열려
        /// 있으면 그중 맨 위, 없으면 _current입니다. 둘 다 없으면 null입니다.</summary>
        private UIPopupBase TopmostPopup => _immediateStack.Count > 0 ? _immediateStack[^1].instance : _current;

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

        public bool IsAnyPopupOpen => _current != null || _immediateStack.Count > 0;
        public bool HasPendingPopups => _pending.Count > 0;
        public bool IsBlockingInput => _current != null || _immediateStack.Count > 0;

        /// <summary>지금 화면에서 가장 위에 있는 팝업(즉시표시 팝업이 있으면 그 팝업, 없으면
        /// 현재 팝업)이 뒤로가기/취소 입력으로 닫혀도 되는지 여부입니다. 팝업이 없으면 false입니다.</summary>
        public bool CurrentPopupCloseableByBackButton => TopmostPopup != null && TopmostPopup.CloseableByBackButton;

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

            UIPopupBase topmost = TopmostPopup;

            if (topmost == null || topmost.CloseableByBackButton == false)
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

                if (IsAlreadyImmediateType(t) == true)
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

            if (policy == EPopupPolicy.Immediate)
            {
                OpenImmediatePopup(req);
                return;
            }

            if (_current != null && _isClosingCurrent == false)
            {
                if (policy == EPopupPolicy.PreemptIfHigher &&
                    priority > (EPopupPriority)_current.OpenPriority)
                {
                    SuspendCurrentToPending();
                    _pending.Add(req);
                    _processScheduled = true;
                    return;
                }

                if (policy == EPopupPolicy.ReplaceCurrent)
                {
                    ClosePopup(_current);

                    if (_forcedNext != null)
                    {
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

        /// <summary>화면상 가장 위(즉시표시 팝업이 있으면 그중 맨 위, 없으면 현재 팝업)를 닫습니다.</summary>
        public void CloseTopPopup(object result = null)
        {
            if (_immediateStack.Count > 0)
            {
                CloseImmediatePopupAt(_immediateStack.Count - 1, result);
                return;
            }

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
            int immediateIndex = FindImmediateIndex(target);
            if (immediateIndex >= 0)
            {
                CloseImmediatePopupAt(immediateIndex, result);
                return;
            }

            if (_current != target || _isClosingCurrent)
            {
                return;
            }

            _isClosingCurrent = true;

            Action<object> onResult = _currentResultCallback;
            _currentResultCallback = null;

            target.RequestClose(() =>
            {
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
                RestackBlocker();

                onResult?.Invoke(result);
            });
        }

        /// <summary>대기열을 즉시 비우고 현재 팝업을 닫습니다 (결과값 전달 없음). 씬 전환 시 정리용입니다.</summary>
        public void CloseAll()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                UIPopupBase suspended = _pending[i].instance;
                if (suspended != null && PoolManager.Instance != null)
                {
                    PoolManager.Instance.Despawn(suspended.gameObject);
                }
            }

            _pending.Clear();

            for (int i = 0; i < _immediateStack.Count; i++)
            {
                UIPopupBase instance = _immediateStack[i].instance;
                if (instance != null && PoolManager.Instance != null)
                {
                    PoolManager.Instance.Despawn(instance.gameObject);
                }
            }

            _immediateStack.Clear();

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

            if (_forcedNext != null)
            {
                PopupRequest forced = _forcedNext;
                _forcedNext = null;
                forced.onResult?.Invoke(null);
            }

            if (_current == null || _isClosingCurrent)
            {
                RestackBlocker();
                return;
            }

            _isClosingCurrent = true;
            UIPopupBase target = _current;
            _currentResultCallback = null;

            target.RequestClose(() =>
            {
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
                RestackBlocker();
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
                _current = instance;
                instance.OnResume(req.payload);
            }
            else
            {
                if (PoolManager.Instance == null)
                {
                    req.onResult?.Invoke(null);
                    return;
                }

                instance = PoolManager.Instance.Spawn(req.prefab, Vector3.zero, Quaternion.identity, _popupRoot);
                if (instance == null)
                {
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
                _current = instance;
                instance.OnOpen(req.payload);
            }

            RestackBlocker();
        }

        private void OpenImmediatePopup(PopupRequest req)
        {
            if (PoolManager.Instance == null)
            {
                req.onResult?.Invoke(null);
                return;
            }

            UIPopupBase instance = PoolManager.Instance.Spawn(req.prefab, Vector3.zero, Quaternion.identity, _popupRoot);
            if (instance == null)
            {
                Debug.LogWarning($"[UIManager] {req.prefab.GetType().Name} 즉시표시 팝업을 스폰하지 못했습니다 (풀이 가득 찼을 수 있습니다). 이 요청은 건너뜁니다.");
                req.onResult?.Invoke(null);
                return;
            }

            AttachToRoot(instance.transform);

            instance.InitializePopupMeta(
                req.prefab.GetType(),
                (int)req.priority,
                req.sequence
            );

            ImmediatePopupEntry entry = new ImmediatePopupEntry
            {
                instance = instance,
                onResult = req.onResult,
            };

            _immediateStack.Add(entry);

            instance.OnOpen(req.payload);

            RestackBlocker();
        }

        private int FindImmediateIndex(UIPopupBase target)
        {
            for (int i = 0; i < _immediateStack.Count; i++)
            {
                if (_immediateStack[i].instance == target)
                {
                    return i;
                }
            }

            return -1;
        }

        private void CloseImmediatePopupAt(int index, object result)
        {
            ImmediatePopupEntry entry = _immediateStack[index];

            if (entry.isClosing)
            {
                return;
            }

            entry.isClosing = true;

            Action<object> onResult = entry.onResult;
            entry.onResult = null;

            entry.instance.RequestClose(() =>
            {
                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Despawn(entry.instance.gameObject);
                }

                _immediateStack.Remove(entry);
                RestackBlocker();
                onResult?.Invoke(result);
            });
        }

        private void RestackBlocker()
        {
            UIPopupBase topmost = TopmostPopup;

            if (topmost == null)
            {
                _modalBlocker.SetActive(false);
                return;
            }

            _modalBlocker.SetActive(true);
            _modalBlocker.transform.SetAsLastSibling();
            topmost.transform.SetAsLastSibling();
        }

        // ---- 토스트 ----

        /// <summary>비모달이며 자동으로 사라지는 토스트를 표시합니다. 여러 토스트가 동시에 표시될 수 있고 입력을 절대 막지 않습니다.</summary>
        public void ShowToast(UIToastBase prefab, object payload = null, float duration = -1f)
        {
            if (prefab == null)
            {
                return;
            }

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

        private bool IsAlreadyImmediateType(Type t)
        {
            for (int i = 0; i < _immediateStack.Count; i++)
            {
                if (_immediateStack[i].instance.PopupType == t)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
