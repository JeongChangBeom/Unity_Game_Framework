using System;
using System.Threading;
using UnityEngine;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 기본 로딩 화면의 페이드 연출입니다. UI 패키지의 DefaultUIAnimation은 internal이라
    /// 이 패키지에서 재사용할 수 없어, 같은 형태로 별도 구현합니다.
    /// </summary>
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
            group.alpha = from;

            float t = 0f;

            try
            {
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
                // 새 Show/Hide 요청이 이 애니메이션을 밀어냈습니다. 새 요청이 자기 자신의
                // onComplete를 따로 호출하므로, 여기서는 부르지 않고 조용히 반환합니다.
                return;
            }
            catch (Exception e)
            {
                // group이 파괴되는 등 예상 밖의 예외가 나도, 이 onComplete를 기다리고 있는
                // AwaitableCompletionSource(ShowLoadingScreenAsync/HideLoadingScreenAsync)가
                // 영원히 풀리지 않으면 안 되므로 로그만 남기고 아래에서 onComplete는 호출합니다.
                Debug.LogError($"[SceneLoading] 페이드 애니메이션 중 예외가 발생했습니다: {e}");
            }

            onComplete?.Invoke();
        }
    }
}
