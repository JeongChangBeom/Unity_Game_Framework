using System;
using System.Threading;
using UnityEngine;

namespace GameFramework.SceneLoading
{
    /// <summary>기본 로딩 화면의 페이드 연출입니다.</summary>
    internal static class DefaultSceneLoadingAnimation
    {
        public static async Awaitable FadeTo(
            CanvasGroup group,
            float from,
            float to,
            float duration,
            CancellationToken cancellationToken,
            Action onComplete)
        {
            float t = 0f;

            try
            {
                group.alpha = from;

                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float s = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                    group.alpha = s;
                    await Awaitable.NextFrameAsync(cancellationToken);
                }

                group.alpha = to;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoading] 페이드 애니메이션 중 예외가 발생했습니다: {e}");
            }

            onComplete?.Invoke();
        }
    }
}
