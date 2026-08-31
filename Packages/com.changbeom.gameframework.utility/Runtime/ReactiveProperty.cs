using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Utility
{
    /// <summary>
    /// 값이 바뀔 때 구독자에게 자동으로 알리는 반응형 프로퍼티입니다. UI를 특정 상태(체력,
    /// 재화 등)에 직접 참조로 바인딩할 때 사용합니다 - 값이 바뀌는 통로가 Value 세터
    /// 하나뿐이라 알림을 깜빡할 일이 없습니다.
    /// </summary>
    [Serializable]
    public class ReactiveProperty<T>
    {
        [SerializeField] private T _value;

        public event Action<T> OnValueChanged;

        public ReactiveProperty() { }

        public ReactiveProperty(T initialValue)
        {
            _value = initialValue;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value))
                {
                    return;
                }

                _value = value;
                Notify(_value);
            }
        }

        /// <summary>구독을 시작하면서 현재 값을 즉시 한 번 받고 싶을 때 사용합니다 (UI 초기화 등).</summary>
        public void Subscribe(Action<T> handler, bool invokeImmediately = true)
        {
            OnValueChanged += handler;

            if (invokeImmediately)
            {
                InvokeSafely(handler, _value);
            }
        }

        public void Unsubscribe(Action<T> handler)
        {
            OnValueChanged -= handler;
        }

        /// <summary>구독자를 전부 해제합니다.</summary>
        public void ClearSubscribers()
        {
            OnValueChanged = null;
        }

        private void Notify(T value)
        {
            if (OnValueChanged == null)
            {
                return;
            }

            Delegate[] invocationList = OnValueChanged.GetInvocationList();

            for (int i = 0; i < invocationList.Length; i++)
            {
                InvokeSafely((Action<T>)invocationList[i], value);
            }
        }

        private static void InvokeSafely(Action<T> handler, T value)
        {
            try
            {
                handler?.Invoke(value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReactiveProperty] 구독자 콜백에서 예외가 발생했습니다: {e}");
            }
        }
    }
}
