using System;
using UnityEngine;

public abstract class UIPopupBase : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    public Type PopupType { get; private set; }

    public int OpenPriority { get; private set; }
    public int OpenSequence { get; private set; }

    public object CachedPayload { get; private set; }

    private Action _onClosed;

    public void InitializePopupMeta(Type popupType, int priority, int sequence)
    {
        PopupType = popupType;
        OpenPriority = priority;
        OpenSequence = sequence;
    }

    public virtual void OnOpen(object payload)
    {
        CachedPayload = payload;
        IsOpen = true;
        gameObject.SetActive(true);
    }

    public virtual void OnResume(object payload)
    {
        CachedPayload = payload;
        IsOpen = true;
        gameObject.SetActive(true);
    }

    public virtual void OnSuspend()
    {
        IsOpen = false;
        gameObject.SetActive(false);
    }

    public void RequestClose(Action onClosed)
    {
        _onClosed = onClosed;
        PlayCloseAnimation();
    }

    protected virtual void PlayCloseAnimation()
    {
        CompleteClose();
    }

    protected void CompleteClose()
    {
        IsOpen = false;
        gameObject.SetActive(false);

        _onClosed?.Invoke();
        _onClosed = null;
    }

    public virtual void OnBeforeReturnToPool()
    {
        // 풀에 반환되기 전 처리
    }

    public virtual void OnAfterGetFromPool()
    {
        // 풀에서 가져온 후 처리
    }

    public void CloseSelf()
    {
        if (UIManager.Instance == null)
        {
            Destroy(gameObject);
            return;
        }

        UIManager.Instance.ClosePopup(this);
    }
}
