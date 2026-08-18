// 자동 생성됨. 직접 편집하지 마세요.

using System;
using UnityEngine;
using GameFramework.Pooling;
using GameFramework.UISystem;

namespace GameFramework.Pooling
{
    public enum EPoolKey
    {
        None = 0,
        Test,
        UIPopup_TestA,
        UIPopup_TestB,
    }

    // PoolManager/UIManager는 패키지 쪽 코드라 이 프로젝트 전용 enum을 컴파일
    // 타임에 알 수 없습니다. 대신 string 기반 API를 감싸는 확장 메서드로 강타입
    // 호출부(pm.Spawn(EPoolKey.X, ...), uiManager.RequestPopup(EPoolKey.X, ...))를
    // 그대로 제공합니다.
    public static class EPoolKeyExtensions
    {
        public static GameObject Spawn(this PoolManager manager, EPoolKey key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (key == EPoolKey.None)
            {
                Debug.LogError("[EPoolKeyExtensions] EPoolKey.None으로는 Spawn할 수 없습니다.");
                return null;
            }

            return manager.Spawn(key.ToString(), position, rotation, parent);
        }

        public static T Spawn<T>(this PoolManager manager, EPoolKey key, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (key == EPoolKey.None)
            {
                Debug.LogError("[EPoolKeyExtensions] EPoolKey.None으로는 Spawn할 수 없습니다.");
                return null;
            }

            return manager.Spawn<T>(key.ToString(), position, rotation, parent);
        }

        public static void RequestPopup(this UIManager uiManager, EPoolKey key, EPopupPriority priority, object payload = null, bool unique = true, EPopupPolicy policy = EPopupPolicy.PreemptIfHigher, Action<object> onResult = null)
        {
            uiManager.RequestPopup(key.ToString(), priority, payload, unique, policy, onResult);
        }

        public static void RequestPopup<TResult>(this UIManager uiManager, EPoolKey key, EPopupPriority priority, Action<TResult> onResult, object payload = null, bool unique = true, EPopupPolicy policy = EPopupPolicy.PreemptIfHigher)
        {
            uiManager.RequestPopup(key.ToString(), priority, onResult, payload, unique, policy);
        }
    }
}
