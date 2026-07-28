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

        [SerializeField] private bool _dontDestroyOnLoad = true;

        private bool _initialized;

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

                Debug.LogWarning($"[MonoSingleton] No instance of {typeof(T).Name} found in the scene. Auto-creating one. If initialization order matters, place it explicitly in a boot scene instead.");

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

            if (_dontDestroyOnLoad)
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
