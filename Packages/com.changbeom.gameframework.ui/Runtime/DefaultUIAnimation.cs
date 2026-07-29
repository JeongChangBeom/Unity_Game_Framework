using System;
using System.Threading;
using UnityEngine;

namespace GameFramework.UISystem
{
    /// <summary>
    /// 프레임워크의 기본 open/close/show/hide 연출로 사용되는 임시 스케일 애니메이션입니다.
    /// 모든 팝업/토스트가 기본으로 *어떤* 연출이라도 갖도록 하기 위한 것이며, 각
    /// PlayXAnimation() 호출부에서 원하는 애니메이션 코드로 교체하는 것을 전제로 합니다.
    /// </summary>
    internal static class DefaultUIAnimation
    {
        public static async Awaitable ScaleTo(
            Transform target,
            float from,
            float to,
            float duration,
            CancellationToken cancellationToken,
            Action onComplete)
        {
            target.localScale = Vector3.one * from;

            float t = 0f;

            try
            {
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float s = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                    target.localScale = Vector3.one * s;
                    await Awaitable.NextFrameAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            target.localScale = Vector3.one * to;
            onComplete?.Invoke();
        }
    }
}
