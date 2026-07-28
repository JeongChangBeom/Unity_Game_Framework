using System;
using GameFramework.Pooling;
using UnityEngine;

namespace GameFramework.UISystem
{
    public abstract class UIPopupBase : MonoBehaviour, IPoolable
    {
        [SerializeField] private float _animationDuration = 0.2f;

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

        protected virtual void PlayOpenAnimation()
        {
            // 기본: 스케일 0 -> 1로 커지면서 열림.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteOpen()을 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 0f, 1f, _animationDuration, destroyCancellationToken, CompleteOpen);
        }

        protected virtual void CompleteOpen()
        {
            // 열림 연출이 끝난 뒤 추가 처리가 필요하면 override 하세요.
        }

        public virtual void OnResume(object payload)
        {
            CachedPayload = payload;
            IsOpen = true;
            gameObject.SetActive(true);
            PlayResumeAnimation();
        }

        protected virtual void PlayResumeAnimation()
        {
            // 기본: Open과 동일하게 스케일 0 -> 1로 커지면서 재개.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteResume()을 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 0f, 1f, _animationDuration, destroyCancellationToken, CompleteResume);
        }

        protected virtual void CompleteResume()
        {
            // 재개 연출이 끝난 뒤 추가 처리가 필요하면 override 하세요.
        }

        public virtual void OnSuspend()
        {
            PlaySuspendAnimation();
        }

        protected virtual void PlaySuspendAnimation()
        {
            // 기본: 스케일 1 -> 0으로 작아지면서 숨김.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteSuspend()를 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 1f, 0f, _animationDuration, destroyCancellationToken, CompleteSuspend);
        }

        protected void CompleteSuspend()
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
            // 기본: 스케일 1 -> 0으로 작아지면서 닫힘.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteClose()를 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 1f, 0f, _animationDuration, destroyCancellationToken, CompleteClose);
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
