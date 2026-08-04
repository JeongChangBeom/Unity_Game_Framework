using System;
using System.Threading;
using UnityEngine;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 씬 로드와 나란히(동시에) 진행되는 가중치 있는 비동기 작업 한 단계입니다. 예를 들어
    /// 다음 씬에 필요한 에셋을 미리 로드하는 작업을 씬 로드와 겹쳐서 진행하면서, 그 진행률까지
    /// 하나의 SceneLoadingManager.Progress에 가중 합산해 보여줄 때 사용합니다.
    /// </summary>
    public readonly struct SceneLoadStep
    {
        public string Label { get; }
        public float Weight { get; }

        /// <summary>
        /// true(기본값)면 이 단계가 실패할 때 씬 로드 전체를 실패로 취급합니다(재시도/폴백 대상).
        /// false면 경고 로그만 남기고 씬 로드는 계속 진행합니다.
        /// </summary>
        public bool Critical { get; }

        /// <summary>진행률(0~1) 보고 콜백과 취소 토큰을 받아 실행할 작업입니다.</summary>
        public Func<Action<float>, CancellationToken, Awaitable> RunAsync { get; }

        public SceneLoadStep(string label, float weight, Func<Action<float>, CancellationToken, Awaitable> runAsync, bool critical = true)
        {
            Label = string.IsNullOrEmpty(label) ? "(unnamed step)" : label;
            Weight = Mathf.Max(0f, weight);
            RunAsync = runAsync ?? throw new ArgumentNullException(nameof(runAsync));
            Critical = critical;
        }
    }
}
