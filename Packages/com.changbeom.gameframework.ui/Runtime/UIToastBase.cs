using System;
using GameFramework.Pooling;
using UnityEngine;

namespace GameFramework.UISystem
{
    /// <summary>
    /// Non-modal, auto-dismissing notification (e.g. "Item obtained"). Unlike UIPopupBase,
    /// multiple toasts can be visible at once and they never block input.
    /// </summary>
    public abstract class UIToastBase : MonoBehaviour, IPoolable
    {
        [SerializeField] private float _animationDuration = 0.2f;

        private Action _onHidden;

        public virtual void Show(object payload)
        {
            gameObject.SetActive(true);
            PlayShowAnimation();
        }

        protected virtual void PlayShowAnimation()
        {
            // 기본: 스케일 0 -> 1로 커지면서 표시.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteShow()를 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 0f, 1f, _animationDuration, destroyCancellationToken, CompleteShow);
        }

        protected virtual void CompleteShow()
        {
            // 표시 연출이 끝난 뒤 추가 처리가 필요하면 override 하세요.
        }

        /// <summary>Called by UIManager. Plays the hide animation, then invokes onHidden (which despawns this toast).</summary>
        public void RequestHide(Action onHidden)
        {
            _onHidden = onHidden;
            PlayHideAnimation();
        }

        protected virtual void PlayHideAnimation()
        {
            // 기본: 스케일 1 -> 0으로 작아지면서 숨김.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteHide()를 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 1f, 0f, _animationDuration, destroyCancellationToken, CompleteHide);
        }

        protected void CompleteHide()
        {
            gameObject.SetActive(false);

            _onHidden?.Invoke();
            _onHidden = null;
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

        /// <summary>Dismisses this toast early (e.g. a tap-to-dismiss button), instead of waiting for its auto-hide timer.</summary>
        public void HideSelf()
        {
            if (UIManager.Instance == null)
            {
                Destroy(gameObject);
                return;
            }

            UIManager.Instance.HideToast(this);
        }
    }
}
