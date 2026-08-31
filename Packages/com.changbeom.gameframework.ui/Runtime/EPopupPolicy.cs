namespace GameFramework.UISystem
{
    public enum EPopupPolicy
    {
        QueueOnly,        // 무조건 대기
        PreemptIfHigher,  // 더 높은 priority면 선점
        ReplaceCurrent,   // 현재 팝업 즉시 닫고 교체
        Immediate,        // 대기열/현재 팝업과 무관하게 즉시 맨 위에 별도로 띄움 (여러 개 중첩 가능)
    }
}