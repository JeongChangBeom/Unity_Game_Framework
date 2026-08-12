using System;
using System.Threading;
using GameFramework.Pooling;
using UnityEngine;

namespace GameFramework.UISystem
{
    /// <summary>
    /// 비모달이며 자동으로 사라지는 알림입니다 (예: "아이템 획득"). UIPopupBase와 달리
    /// 여러 토스트가 동시에 표시될 수 있고 입력을 절대 막지 않습니다.
    /// </summary>
    public abstract class UIToastBase : MonoBehaviour, IPoolable
    {
        [SerializeField] private float _animationDuration = 0.2f;

        // destroyCancellationToken은 실제 Destroy에서만 취소되는데, 풀링된 오브젝트는
        // Destroy가 아니라 SetActive(false)로만 반환됩니다. 애니메이션은 이 자체 토큰을
        // 쓰고, OnBeforeReturnToPool에서 확실히 취소해서 재활용 후에도 살아남지 않게 합니다.
        private CancellationTokenSource _animCts = new CancellationTokenSource();
        protected CancellationToken AnimationToken => _animCts.Token;

        /// <summary>이 인스턴스의 현재 생애(스폰~풀 반환)에서만 유효한 토큰입니다. 애니메이션뿐
        /// 아니라, 이 토스트 인스턴스에 묶여 있지만 클래스 밖(UIManager의 자동 숨김 타이머 등)에서
        /// 도는 비동기 작업도 재활용 이후까지 살아남으면 안 되므로 이 토큰으로 취소하세요.</summary>
        public CancellationToken DespawnToken => _animCts.Token;

        private Action _onHidden;

        public virtual void Show(object payload)
        {
            RestartAnimationToken();
            gameObject.SetActive(true);
            PlayShowAnimation();
        }

        protected virtual void PlayShowAnimation()
        {
            // 기본: 스케일 0 -> 1로 커지면서 표시.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteShow()를 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 0f, 1f, _animationDuration, AnimationToken, CompleteShow);
        }

        protected virtual void CompleteShow()
        {
            // 표시 연출이 끝난 뒤 추가 처리가 필요하면 override 하세요.
        }

        /// <summary>UIManager가 호출합니다. 숨김 연출을 재생한 뒤 onHidden을 호출합니다 (이 토스트를 디스폰합니다).</summary>
        public void RequestHide(Action onHidden)
        {
            RestartAnimationToken();
            _onHidden = onHidden;
            PlayHideAnimation();
        }

        protected virtual void PlayHideAnimation()
        {
            // 기본: 스케일 1 -> 0으로 작아지면서 숨김.
            // TODO: 원하는 연출로 바꾸려면 이 메서드를 override 하세요.
            //       연출이 끝나면 반드시 CompleteHide()를 호출해야 합니다.
            _ = DefaultUIAnimation.ScaleTo(transform, 1f, 0f, _animationDuration, AnimationToken, CompleteHide);
        }

        protected void CompleteHide()
        {
            gameObject.SetActive(false);

            _onHidden?.Invoke();
            _onHidden = null;
        }

        public virtual void OnBeforeReturnToPool()
        {
            // 풀에 반환되기 전 처리. 진행 중이던 애니메이션이 이 인스턴스가 다른
            // 토스트로 재활용된 뒤에도 계속 돌다가 CompleteX()를 부르는 걸 막기 위해
            // 반드시 취소합니다. override하는 경우 base.OnBeforeReturnToPool()을 호출하세요.
            RestartAnimationToken();
        }

        // Show/RequestHide는 같은 인스턴스 안에서(풀 반환 없이) 서로를 가로챌 수 있습니다
        // (예: duration이 매우 짧거나 0으로 호출되어 Hide가 Show 애니메이션이 끝나기 전에
        // 시작되는 경우, 혹은 HideSelf로 수동 조기 종료). 풀 반환 시점에만 토큰을 갱신하면
        // 먼저 시작한 Show와 나중에 시작한 Hide가 같은 프레임 동안 같은 Transform.localScale을
        // 동시에 애니메이션하며 서로의 결과를 덮어씁니다. UIPopupBase와 동일하게 전환
        // 시작 시점마다 토큰을 갱신해 이전 애니메이션을 취소합니다.
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

        /// <summary>자동 숨김 타이머를 기다리지 않고 이 토스트를 즉시 닫습니다 (예: 탭해서 닫기 버튼).</summary>
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
