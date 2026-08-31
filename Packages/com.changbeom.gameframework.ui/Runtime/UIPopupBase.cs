using System;
using System.Threading;
using GameFramework.Pooling;
using UnityEngine;

namespace GameFramework.UISystem
{
    public abstract class UIPopupBase : MonoBehaviour, IPoolable
    {
        [SerializeField] private float _animationDuration = 0.2f;

        private CancellationTokenSource _animCts = new CancellationTokenSource();
        protected CancellationToken AnimationToken => _animCts.Token;

        public bool IsOpen { get; private set; }

        public Type PopupType { get; private set; }

        public int OpenPriority { get; private set; }
        public int OpenSequence { get; private set; }

        public object CachedPayload { get; private set; }

        /// <summary>뒤로가기 / Escape 키를 무시하려면 false로 설정하세요 (예: 반드시 확인해야 하는 팝업).</summary>
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
            RestartAnimationToken();
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
            _ = DefaultUIAnimation.ScaleTo(transform, 0f, 1f, _animationDuration, AnimationToken, CompleteOpen);
        }

        protected virtual void CompleteOpen()
        {
            // 열림 연출이 끝난 뒤 추가 처리가 필요하면 override 하세요.
        }

        public virtual void OnResume(object payload)
        {
            RestartAnimationToken();
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
            _ = DefaultUIAnimation.ScaleTo(transform, 0f, 1f, _animationDuration, AnimationToken, CompleteResume);
        }

        protected virtual void CompleteResume()
        {
            // 재개 연출이 끝난 뒤 추가 처리가 필요하면 override 하세요.
        }

        public virtual void OnSuspend()
        {
            RestartAnimationToken();
            PlaySuspendAnimation();
        }

        protected virtual void PlaySuspendAnimation()
        {
            // 기본: 스케일 1 -> 0으로 작아지면서 숨김.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteSuspend()를 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 1f, 0f, _animationDuration, AnimationToken, CompleteSuspend);
        }

        protected void CompleteSuspend()
        {
            IsOpen = false;
            gameObject.SetActive(false);
        }

        public void RequestClose(Action onClosed)
        {
            RestartAnimationToken();
            _onClosed = onClosed;
            PlayCloseAnimation();
        }

        protected virtual void PlayCloseAnimation()
        {
            // 기본: 스케일 1 -> 0으로 작아지면서 닫힘.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteClose()를 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 1f, 0f, _animationDuration, AnimationToken, CompleteClose);
        }

        protected void CompleteClose()
        {
            IsOpen = false;
            gameObject.SetActive(false);

            _onClosed?.Invoke();
            _onClosed = null;
        }

        /// <summary>풀에 반환되기 전 처리. override하는 경우 base.OnBeforeReturnToPool()을 호출하세요.</summary>
        public virtual void OnBeforeReturnToPool()
        {
            RestartAnimationToken();
        }

        private void RestartAnimationToken()
        {
            _animCts.Cancel();
            _animCts.Dispose();
            _animCts = new CancellationTokenSource();
        }

        public virtual void OnAfterGetFromPool()
        {
            // 풀에서 가져온 후 처리
        }

        void IPoolable.OnSpawn() => OnAfterGetFromPool();
        void IPoolable.OnDespawn() => OnBeforeReturnToPool();

        /// <summary>결과값 없이 이 팝업을 닫습니다. UnityEvent(예: Button.onClick)에 바로 연결할 수 있도록 매개변수 없이 유지합니다.</summary>
        public void CloseSelf()
        {
            CloseSelf(null);
        }

        /// <summary>이 팝업을 닫습니다. <paramref name="result"/>는 onResult 콜백이 주어졌다면 RequestPopup 호출자에게 전달됩니다.</summary>
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
