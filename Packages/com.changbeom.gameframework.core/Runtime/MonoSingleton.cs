using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core
{
    internal static class MonoSingletonResetHook
    {
        private static readonly List<Action> _resetActions = new List<Action>();

        internal static void Register(Action resetAction)
        {
            _resetActions.Add(resetAction);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            for (int i = 0; i < _resetActions.Count; i++)
            {
                _resetActions[i]?.Invoke();
            }
        }
    }

    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static bool _isQuitting;
        private static bool _resetHookRegistered;

        private bool _initialized;

        /// <summary>
        /// 씬 전환 시 이 싱글톤이 파괴되어야 한다면 false를 반환하도록 오버라이드하세요.
        /// 기본값은 true이며, 일반적인 매니저 동작 방식과 동일합니다.
        /// </summary>
        protected virtual bool ShouldPersistAcrossScenes => true;

        public static T Instance
        {
            get
            {
                if (_isQuitting)
                {
                    return null;
                }

                if (_instance != null)
                {
                    return _instance;
                }

                T found = UnityEngine.Object.FindFirstObjectByType<T>();
                if (found != null)
                {
                    _instance = found;
                    _instance.EnsureInitialized();
                    return _instance;
                }

                Debug.Log($"[MonoSingleton] {typeof(T).Name}을(를) 자동 생성합니다 (씬에 배치된 인스턴스가 없음). 다른 매니저보다 먼저/나중에 초기화돼야 한다면 클래스에 [BootPriority]를 추가하세요.");

                GameObject go = new GameObject(typeof(T).Name);
                _instance = go.AddComponent<T>();
                _instance.EnsureInitialized();
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            RegisterResetHook();

            if (ShouldPersistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            OnInitialize();
        }

        private static void RegisterResetHook()
        {
            if (_resetHookRegistered)
            {
                return;
            }

            _resetHookRegistered = true;

            MonoSingletonResetHook.Register(() =>
            {
                _instance = null;
                _isQuitting = false;
            });
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnApplicationQuit()
        {
            _isQuitting = true;
        }
    }
}
