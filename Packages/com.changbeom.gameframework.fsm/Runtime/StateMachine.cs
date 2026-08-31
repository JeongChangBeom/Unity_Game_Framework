using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.FSM
{
    /// <summary>
    /// 상태 키(TStateKey, 보통 enum)마다 하나씩 등록해두고 전환하며 쓰는 순수 C# 상태
    /// 머신입니다. MonoSingleton이 아니라 직접 인스턴스를 만들어 쓰는 도구입니다 -
    /// 몬스터 AI, 캐릭터 행동, 게임 전체 흐름처럼 여러 개를 동시에 각자 따로 쓰는
    /// 용도이기 때문입니다. 완전히 동기로 동작하며(Awaitable 없음), 전이 규칙을
    /// 강제하지 않습니다 - 어떤 상태로든 자유롭게 전환할 수 있고, 특정 상태를 지금
    /// 벗어나면 안 된다면 그 상태의 CanExit을 false로 두면 됩니다. Update는 이 클래스가
    /// 스스로 매 프레임 돌리지 않고, 호출자가 자기 Update()/FixedUpdate()에서 직접
    /// 넘겨줘야 합니다.
    /// </summary>
    public class StateMachine<TStateKey>
    {
        private readonly Dictionary<TStateKey, IState> _states = new Dictionary<TStateKey, IState>();

        private IState _currentState;
        private TStateKey _currentKey;
        private bool _isTransitioning;

        public TStateKey CurrentKey => _currentKey;

        /// <summary>현재 상태가 없거나(아직 ChangeState를 한 번도 안 함), 있다면 그 상태의 CanExit입니다.</summary>
        public bool CanChangeState => _currentState == null || _currentState.CanExit;

        /// <summary>상태가 바뀔 때마다 (이전 키, 다음 키) 순서로 발행됩니다.</summary>
        public event Action<TStateKey, TStateKey> OnStateChanged;

        public void AddState(TStateKey key, IState state)
        {
            if (state == null)
            {
                Debug.LogError($"[StateMachine] {key}에 등록하려는 state가 null입니다.");
                return;
            }

            if (_states.ContainsKey(key))
            {
                Debug.LogWarning($"[StateMachine] {key} 상태가 이미 등록되어 있어 덮어씁니다.");
            }

            _states[key] = state;
        }

        /// <summary>
        /// key로 등록된 상태로 전환합니다. 등록되지 않은 key거나 현재 상태의
        /// CanExit이 false면 무시됩니다. 이미 있는 상태로 다시 전환해도(자기 자신으로
        /// 전환) 특별 취급 없이 OnExit -> OnEnter가 그대로 다시 실행됩니다 - 같은
        /// 행동을 처음부터 다시 재생해야 하는 경우(연타 등)를 지원하기 위함입니다.
        /// OnEnter/OnExit 도중에 다시 호출하면(재진입) 무시됩니다 - 상태 진입 중
        /// 조건에 따라 즉시 다른 상태로 리다이렉트하고 싶다면 OnEnter가 아니라
        /// Update에서 판단해 호출하세요.
        /// </summary>
        public void ChangeState(TStateKey key)
        {
            if (!_states.TryGetValue(key, out IState nextState))
            {
                Debug.LogError($"[StateMachine] {key} 상태가 등록되어 있지 않습니다. AddState로 먼저 등록하세요.");
                return;
            }

            if (!CanChangeState)
            {
                Debug.LogWarning($"[StateMachine] 현재 상태({_currentKey})가 CanExit=false라 {key}(으)로 전환할 수 없습니다.");
                return;
            }

            if (_isTransitioning)
            {
                Debug.LogWarning($"[StateMachine] OnEnter/OnExit/OnStateChanged 도중에는 ChangeState({key})를 호출할 수 없습니다 (재진입 방지).");
                return;
            }

            bool hadPreviousState = _currentState != null;
            TStateKey previousKey = _currentKey;

            _isTransitioning = true;

            try
            {
                _currentState?.OnExit();
                _currentState = nextState;
                _currentKey = key;
                _currentState.OnEnter();

                if (hadPreviousState)
                {
                    InvokeStateChanged(previousKey, key);
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private void InvokeStateChanged(TStateKey previousKey, TStateKey key)
        {
            if (OnStateChanged == null)
            {
                return;
            }

            Delegate[] handlers = OnStateChanged.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<TStateKey, TStateKey>)handlers[i]).Invoke(previousKey, key);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[StateMachine] OnStateChanged 구독자에서 예외가 발생했습니다: {e}");
                }
            }
        }

        public void Update(float deltaTime)
        {
            _currentState?.OnUpdate(deltaTime);
        }
    }
}
