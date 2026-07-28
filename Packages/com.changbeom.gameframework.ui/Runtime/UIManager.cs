using System;
using System.Collections.Generic;
using GameFramework.Core;
using GameFramework.Pooling;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.UISystem
{
    public class UIManager : MonoSingleton<UIManager>
    {
        private UIManagerSettings _settings;

        private Canvas _uiRootCanvas;
        private RectTransform _hudRoot;
        private RectTransform _popupRoot;
        private RectTransform _toastRoot;
        private RectTransform _overlayRoot;
        private GameObject _modalBlocker;

        private readonly List<PopupRequest> _pending = new();
        private readonly HashSet<UIToastBase> _activeToasts = new();

        private UIPopupBase _current;
        private Action<object> _currentResultCallback;

        private int _sequenceCounter;
        private bool _processScheduled;

        /// <summary>Persistent layer for always-visible HUD elements, below popups. Parent your own HUD prefabs here.</summary>
        public Transform HudRoot => _hudRoot;

        /// <summary>Topmost layer, above popups and toasts. For full-screen loading/transition content.</summary>
        public Transform OverlayRoot => _overlayRoot;

        public bool IsAnyPopupOpen => _current != null;
        public bool HasPendingPopups => _pending.Count > 0;
        public bool IsBlockingInput => _current != null;

        protected override void OnInitialize()
        {
            DontDestroyOnLoad(gameObject);

            _settings = LoadSettings();

            EnsureCanvasRoot();
            EnsureLayers();
            EnsureModalBlocker();
        }

        private static UIManagerSettings LoadSettings()
        {
            UIManagerSettings settings = Resources.Load<UIManagerSettings>(UIManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[UIManager] No UIManagerSettings asset found at Resources/{UIManagerSettings.ResourcePath}. Using defaults. Create one via Assets/Create/Game Framework/UI/UI Manager Settings.");
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

        // ---- Popup ----

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
            req.unique = unique;
            req.onResult = onResult;
            req.sequence = _sequenceCounter;
            _sequenceCounter++;

            if (_current != null)
            {
                if (policy == EPopupPolicy.PreemptIfHigher &&
                    priority > (EPopupPriority)_current.OpenPriority)
                {
                    SuspendCurrentToPending();
                    OpenRequestNow(req);
                    return;
                }

                if (policy == EPopupPolicy.ReplaceCurrent)
                {
                    ClosePopup(_current);
                    _pending.Insert(0, req);
                    return;
                }
            }

            _pending.Add(req);
            _processScheduled = true;
        }

        /// <summary>Typed convenience wrapper for popups that report a result (e.g. a confirm/cancel dialog).</summary>
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

        /// <summary><paramref name="result"/> is delivered to whatever onResult callback was passed to RequestPopup for this popup.</summary>
        public void ClosePopup(UIPopupBase target, object result)
        {
            if (_current != target)
            {
                return;
            }

            _modalBlocker.SetActive(false);

            Action<object> onResult = _currentResultCallback;
            _currentResultCallback = null;

            target.RequestClose(() =>
            {
                PoolManager.Instance.Despawn(target.gameObject);
                _current = null;
                _processScheduled = true;
                onResult?.Invoke(result);
            });
        }

        /// <summary>Immediately clears the pending queue and closes the current popup (no result delivered). For scene-transition cleanup.</summary>
        public void CloseAll()
        {
            _pending.Clear();

            if (_current == null)
            {
                return;
            }

            UIPopupBase target = _current;
            _current = null;
            _currentResultCallback = null;
            _modalBlocker.SetActive(false);

            target.RequestClose(() =>
            {
                PoolManager.Instance.Despawn(target.gameObject);
            });
        }

        private void ProcessPending()
        {
            if (_current != null)
            {
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
            r.unique = true;
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
                instance.OnResume(req.payload);
            }
            else
            {
                instance = PoolManager.Instance.Spawn(req.prefab, Vector3.zero, Quaternion.identity, _popupRoot);
                if (instance == null)
                {
                    return;
                }

                AttachToRoot(instance.transform);

                instance.InitializePopupMeta(
                    req.prefab.GetType(),
                    (int)req.priority,
                    req.sequence
                );

                _currentResultCallback = req.onResult;
                instance.OnOpen(req.payload);
            }

            _modalBlocker.SetActive(true);
            _modalBlocker.transform.SetAsLastSibling();
            instance.transform.SetAsLastSibling();

            _current = instance;
        }

        // ---- Toast ----

        /// <summary>Shows a non-modal, auto-dismissing toast. Multiple toasts can be visible at once and they never block input.</summary>
        public void ShowToast(UIToastBase prefab, object payload = null, float duration = -1f)
        {
            if (prefab == null)
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
            _ = AutoHideToastAfterDelay(instance, d);
        }

        /// <summary>Dismisses a toast early. Safe to call even if it already auto-hid.</summary>
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

            toast.RequestHide(() => PoolManager.Instance.Despawn(toast.gameObject));
        }

        private async Awaitable AutoHideToastAfterDelay(UIToastBase toast, float delay)
        {
            await Awaitable.WaitForSecondsAsync(Mathf.Max(0.01f, delay));
            HideToast(toast);
        }

        // ---- Layout ----

        private void EnsureCanvasRoot()
        {
            if (_uiRootCanvas == null)
            {
                _uiRootCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            }

            if (_uiRootCanvas != null)
            {
                return;
            }

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
