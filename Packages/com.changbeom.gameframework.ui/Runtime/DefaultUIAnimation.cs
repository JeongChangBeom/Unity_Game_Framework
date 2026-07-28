using System;
using System.Threading;
using UnityEngine;

namespace GameFramework.UISystem
{
    /// <summary>
    /// Placeholder scale animation used as the framework's default open/close/show/hide feedback.
    /// It exists so every popup/toast has *some* feedback out of the box -- it is meant to be
    /// replaced with your own animation code at each PlayXAnimation() call site.
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
