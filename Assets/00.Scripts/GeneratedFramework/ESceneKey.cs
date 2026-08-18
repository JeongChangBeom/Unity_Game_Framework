// 자동 생성됨. 직접 편집하지 마세요.

using System.Collections.Generic;
using UnityEngine;
using GameFramework.SceneLoading;

namespace GameFramework.SceneLoading
{
    public enum ESceneKey
    {
        None = 0,
        TestScene,
        TestScene2,
    }

    // SceneLoadingManager는 패키지 쪽 코드라 이 프로젝트 전용 enum을 컴파일 타임에
    // 알 수 없습니다. 대신 string 기반 API(LoadSceneAsync(string, ...))를 감싸는
    // 확장 메서드로 강타입 호출부(sm.LoadSceneAsync(ESceneKey.X))를 그대로 제공합니다.
    public static class ESceneKeyExtensions
    {
        public static Awaitable LoadSceneAsync(this SceneLoadingManager manager, ESceneKey sceneKey)
        {
            return manager.LoadSceneAsync(sceneKey, null);
        }

        public static async Awaitable LoadSceneAsync(this SceneLoadingManager manager, ESceneKey sceneKey, IReadOnlyList<SceneLoadStep> extraSteps)
        {
            if (sceneKey == ESceneKey.None)
            {
                // 확장 메서드에서는 SceneLoadingManager.OnSceneLoadFailed를 직접 발행할 수
                // 없어(이벤트는 선언한 클래스 밖에서 Invoke 불가), 시도 자체를 막고
                // 에러만 남깁니다.
                Debug.LogError("[ESceneKeyExtensions] ESceneKey.None으로는 씬을 로드할 수 없습니다.");
                return;
            }

            await manager.LoadSceneAsync(sceneKey.ToString(), extraSteps);
        }
    }
}
