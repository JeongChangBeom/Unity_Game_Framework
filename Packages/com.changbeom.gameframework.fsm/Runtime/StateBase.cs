namespace GameFramework.FSM
{
    /// <summary>IState의 기본 구현입니다. 필요한 메서드만 override해서 쓰세요. abstract 멤버가
    /// 없어서 아무 동작도 없는 상태(Idle 등)로 바로 인스턴스화해서 쓸 수도 있습니다.</summary>
    public class StateBase : IState
    {
        public virtual bool CanExit => true;

        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnUpdate(float deltaTime) { }
    }
}
