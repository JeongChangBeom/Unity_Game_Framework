namespace GameFramework.FSM
{
    /// <summary>
    /// StateMachine&lt;TStateKey&gt;에 등록되는 상태 하나입니다. 직접 구현하는 대신
    /// StateBase를 상속하면 필요한 것만 override하면 됩니다.
    /// </summary>
    public interface IState
    {
        void OnEnter();
        void OnExit();
        void OnUpdate(float deltaTime);

        /// <summary>
        /// false면 이 상태가 활성 상태인 동안 ChangeState 요청이 전부 무시됩니다
        /// (예: 공격/구르기 애니메이션의 캔슬 불가 구간, 점프 중 착지 전). 애니메이션
        /// 이벤트나 착지 감지 등 외부 신호로 다시 true로 바꿔주면 그때부터 전환할 수
        /// 있습니다.
        /// </summary>
        bool CanExit { get; }
    }
}
