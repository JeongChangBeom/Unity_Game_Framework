using System;
using System.Threading;
using UnityEngine;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 씬 로딩 화면의 기본 클래스입니다. 팝업/토스트와 달리 앱 수명 내내 유지되는 단일
    /// 인스턴스만 존재하므로 풀링 대상이 아닙니다. override해서 원하는 연출로 바꾸세요.
    /// </summary>
    public abstract class SceneLoadingScreenBase : MonoBehaviour
    {
        [SerializeField] private float _fadeDuration = 0.25f;

        private CancellationTokenSource _animCts = new CancellationTokenSource();
        protected CancellationToken AnimationToken => _animCts.Token;

        public bool IsVisible { get; private set; }

        protected float FadeDuration => _fadeDuration;

        private Action _onShown;
        private Action _onHidden;

        internal void ApplySettings(float fadeDuration)
        {
            _fadeDuration = fadeDuration;
        }

        public void RequestShow(Action onShown)
        {
            FlushPendingCallback();
            RestartAnimationToken();
            _onShown = onShown;
            gameObject.SetActive(true);
            PlayShow();
        }

        protected virtual void PlayShow()
        {
            // 기본: CanvasGroup 알파 0 -> 1로 페이드 인.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteShow()를 호출해야 합니다.
            _ = DefaultSceneLoadingAnimation.FadeTo(GetOrAddCanvasGroup(), 0f, 1f, _fadeDuration, AnimationToken, CompleteShow);
        }

        protected void CompleteShow()
        {
            IsVisible = true;

            Action onShown = _onShown;
            _onShown = null;
            onShown?.Invoke();
        }

        public void RequestHide(Action onHidden)
        {
            FlushPendingCallback();
            RestartAnimationToken();
            _onHidden = onHidden;
            PlayHide();
        }

        protected virtual void PlayHide()
        {
            // 기본: CanvasGroup 알파 1 -> 0으로 페이드 아웃.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteHide()를 호출해야 합니다.
            _ = DefaultSceneLoadingAnimation.FadeTo(GetOrAddCanvasGroup(), 1f, 0f, _fadeDuration, AnimationToken, CompleteHide);
        }

        protected void CompleteHide()
        {
            IsVisible = false;
            gameObject.SetActive(false);

            Action onHidden = _onHidden;
            _onHidden = null;
            onHidden?.Invoke();
        }

        /// <summary>진행률(0~1) 표시가 필요하면 override 하세요. 기본은 아무 동작도 하지 않습니다.</summary>
        public virtual void SetProgress(float progress01)
        {
        }

        protected CanvasGroup GetOrAddCanvasGroup()
        {
            if (!TryGetComponent(out CanvasGroup group))
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private void RestartAnimationToken()
        {
            _animCts.Cancel();
            _animCts.Dispose();
            _animCts = new CancellationTokenSource();
        }

        private void FlushPendingCallback()
        {
            if (_onShown != null)
            {
                CompleteShow();
            }
            else if (_onHidden != null)
            {
                CompleteHide();
            }
        }
    }
}
