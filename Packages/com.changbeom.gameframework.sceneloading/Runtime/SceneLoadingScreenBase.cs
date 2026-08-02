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

        // destroyCancellationToken은 실제 Destroy에서만 취소되는데, 이 인스턴스는 앱 수명
        // 내내 살아남아 Show/Hide를 반복합니다. 풀링된 팝업/토스트에서 "재활용 이후에도
        // 이전 생애의 애니메이션이 살아남는" 버그를 겪은 적이 있어, 여기서는 애초에 매
        // Show/Hide 요청 시작 시점마다 토큰을 갱신해서 이전 전환의 애니메이션을 확실히
        // 취소합니다 (예: 숨김 애니메이션이 끝나기 전에 다시 표시 요청이 들어오는 경우).
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

        // RestartAnimationToken()이 진행 중이던 애니메이션을 취소하면, DefaultSceneLoadingAnimation은
        // 취소된 애니메이션의 onComplete(CompleteShow/CompleteHide)를 부르지 않습니다. 그러면 그
        // 완료를 기다리던 쪽(SceneLoadingManager의 AwaitableCompletionSource)이 영원히 멈추므로,
        // 새 요청이 이전 요청을 밀어내기 전에 이전 콜백을 여기서 즉시 완료 처리해 대기를 풀어줍니다.
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
