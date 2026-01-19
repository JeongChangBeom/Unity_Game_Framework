public enum EPopupPolicy
{
    QueueOnly,        // 무조건 대기
    PreemptIfHigher,  // 더 높은 priority면 선점
    ReplaceCurrent,   // 현재 팝업 즉시 닫고 교체
}