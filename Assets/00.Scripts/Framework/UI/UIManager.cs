using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoSingleton<UIManager>
{
    [Header("Root")]
    [SerializeField] private Canvas _uiRootCanvas;
    [SerializeField] private RectTransform _popupRoot;

    [Header("Modal")]
    [SerializeField] private GameObject _modalBlocker;

    private readonly List<PopupRequest> _pending = new();
    private readonly Dictionary<Type, Stack<UIPopupBase>> _pool = new();

    private UIPopupBase _current;

    private int _sequenceCounter;
    private bool _processScheduled;

    protected override void OnInitialize()
    {
        DontDestroyOnLoad(gameObject);

        EnsureCanvasRoot();
        EnsurePopupRoot();
        EnsureModalBlocker();
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

    public void RequestPopup(
        UIPopupBase prefab,
        EPopupPriority priority,
        object payload = null,
        bool unique = true,
        EPopupPolicy policy = EPopupPolicy.PreemptIfHigher)
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

    public void CloseTopPopup()
    {
        if (_current == null)
        {
            return;
        }

        ClosePopup(_current);
    }

    public void ClosePopup(UIPopupBase target)
    {
        if (_current != target)
        {
            return;
        }

        _modalBlocker.SetActive(false);

        target.RequestClose(() =>
        {
            ReturnToPool(target);
            _current = null;
            _processScheduled = true;
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

        cur.OnSuspend();
        _current = null;

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

            instance.OnResume(req.payload);
        }
        else
        {
            instance = GetFromPoolOrCreate(req.prefab);
            AttachToRoot(instance.transform);

            instance.InitializePopupMeta(
                req.prefab.GetType(),
                (int)req.priority,
                req.sequence
            );

            instance.OnAfterGetFromPool();
            instance.OnOpen(req.payload);
        }

        _modalBlocker.SetActive(true);
        _modalBlocker.transform.SetAsLastSibling();
        instance.transform.SetAsLastSibling();

        _current = instance;
    }

    private UIPopupBase GetFromPoolOrCreate(UIPopupBase prefab)
    {
        Type t = prefab.GetType();

        if (_pool.TryGetValue(t, out var bag) == false)
        {
            bag = new Stack<UIPopupBase>();
            _pool[t] = bag;
        }

        while (bag.Count > 0)
        {
            UIPopupBase p = bag.Pop();
            if (p != null)
            {
                return p;
            }
        }

        return Instantiate(prefab, _popupRoot);
    }

    private void ReturnToPool(UIPopupBase popup)
    {
        popup.OnBeforeReturnToPool();

        Type t = popup.PopupType;

        if (_pool.TryGetValue(t, out var bag) == false)
        {
            bag = new Stack<UIPopupBase>();
            _pool[t] = bag;
        }

        popup.transform.SetParent(_popupRoot, false);
        bag.Push(popup);
    }

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

        _uiRootCanvas = go.AddComponent<Canvas>();
        _uiRootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
    }

    private void EnsurePopupRoot()
    {
        if (_popupRoot != null)
        {
            return;
        }

        GameObject go = new GameObject("[PopupRoot]");
        go.layer = LayerMask.NameToLayer("UI");

        _popupRoot = go.AddComponent<RectTransform>();
        _popupRoot.SetParent(_uiRootCanvas.transform, false);

        _popupRoot.anchorMin = Vector2.zero;
        _popupRoot.anchorMax = Vector2.one;
        _popupRoot.offsetMin = Vector2.zero;
        _popupRoot.offsetMax = Vector2.zero;
    }

    private void EnsureModalBlocker()
    {
        if (_modalBlocker != null)
        {
            return;
        }

        GameObject go = new GameObject("[ModalBlocker]");
        go.transform.SetParent(_popupRoot, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.5f);
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
