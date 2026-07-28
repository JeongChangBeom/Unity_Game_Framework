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
        /// Override to return false if this singleton should NOT survive scene loads.
        /// Defaults to true, matching typical manager behavior.
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

                Debug.Log($"[MonoSingleton] Auto-creating {typeof(T).Name} (no instance was placed in the scene). Add [BootPriority] to the class if it needs to initialize before/after other managers.");

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
