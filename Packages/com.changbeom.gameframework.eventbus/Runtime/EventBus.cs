using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.EventBus
{
    internal static class EventBusResetHook
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

    /// <summary>
    /// 이벤트 타입(TEvent)마다 독립된 구독자 목록을 갖는 전역 발행/구독 채널입니다.
    /// 발행자와 구독자가 서로의 존재를 몰라도 되는 전역/횡단 이벤트에 사용하세요
    /// (보스 처치 -> 업적 반응, 아이템 획득 -> 사운드 재생 등). 특정 오브젝트 하나의
    /// 상태를 그 오브젝트를 보여주는 UI 하나에 묶는 1:1 바인딩에는 Utility 패키지의
    /// ReactiveProperty&lt;T&gt;를 사용하세요 - 이런 경우엔 EventBus보다 더 간단하고
    /// 정확합니다.
    /// </summary>
    public static class EventBus<TEvent>
    {
        private static Action<TEvent> _handlers;

        static EventBus()
        {
            EventBusResetHook.Register(() => _handlers = null);
        }

        public static void Subscribe(Action<TEvent> handler)
        {
            _handlers += handler;
        }

        public static void Unsubscribe(Action<TEvent> handler)
        {
            _handlers -= handler;
        }

        /// <summary>한 번만 받고 자동으로 구독 해제됩니다.</summary>
        public static void SubscribeOnce(Action<TEvent> handler)
        {
            Action<TEvent> wrapper = null;

            wrapper = evt =>
            {
                Unsubscribe(wrapper);
                handler(evt);
            };

            Subscribe(wrapper);
        }

        /// <summary>구독자를 전부 해제합니다.</summary>
        public static void ClearSubscribers()
        {
            _handlers = null;
        }

        /// <summary>구독자 하나가 예외를 던져도 나머지 구독자는 정상적으로 이벤트를 받습니다.</summary>
        public static void Publish(TEvent evt)
        {
            if (_handlers == null)
            {
                return;
            }

            Delegate[] invocationList = _handlers.GetInvocationList();

            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<TEvent>)invocationList[i]).Invoke(evt);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] {typeof(TEvent).Name} 구독자 콜백에서 예외가 발생했습니다: {e}");
                }
            }
        }
    }
}
