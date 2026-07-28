using System;
using GameFramework.Pooling;
using UnityEngine;

namespace GameFramework.UI
{
    public abstract class UIPopupBase : MonoBehaviour, IPoolable
    {
        public bool IsOpen { get; private set; }

        public Type PopupType { get; private set; }

        public int OpenPriority { get; private set; }
        public int OpenSequence { get; private set; }

        public object CachedPayload { get; private set; }

        /// <summary>False to ignore the back button / Escape key (e.g. a mandatory confirmation popup).</summary>
        public virtual bool CloseableByBackButton => true;

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
            PlayOpenAnimation();
        }

        public virtual void OnResume(object payload)
        {
            CachedPayload = payload;
            IsOpen = true;
            gameObject.SetActive(true);
            PlayOpenAnimation();
        }

        public virtual void OnSuspend()
        {
            IsOpen = false;
            gameObject.SetActive(false);
        }

        protected virtual void PlayOpenAnimation()
        {
            // 기본은 즉시 표시. 연출이 필요하면 override.
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

        void IPoolable.OnSpawn() => OnAfterGetFromPool();
        void IPoolable.OnDespawn() => OnBeforeReturnToPool();

        /// <summary>Closes this popup with no result. Kept parameterless so it can be wired directly to a UnityEvent (e.g. Button.onClick).</summary>
        public void CloseSelf()
        {
            CloseSelf(null);
        }

        /// <summary>Closes this popup. <paramref name="result"/> reaches the RequestPopup caller's onResult callback, if one was given.</summary>
        public void CloseSelf(object result)
        {
            if (UIManager.Instance == null)
            {
                Destroy(gameObject);
                return;
            }

            UIManager.Instance.ClosePopup(this, result);
        }
    }
}
