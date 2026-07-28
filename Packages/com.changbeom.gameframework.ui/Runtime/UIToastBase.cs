using GameFramework.Pooling;
using UnityEngine;

namespace GameFramework.UI
{
    /// <summary>
    /// Non-modal, auto-dismissing notification (e.g. "Item obtained"). Unlike UIPopupBase,
    /// multiple toasts can be visible at once and they never block input.
    /// </summary>
    public abstract class UIToastBase : MonoBehaviour, IPoolable
    {
        public virtual void Show(object payload)
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
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
