using System;

namespace GameFramework.Core
{
    /// <summary>
    /// MonoSingleton을 상속하는 매니저의 명시적이고 결정적인 초기화 순서를 선언합니다.
    /// 값이 작을수록 먼저 초기화됩니다. 이 attribute가 없는 타입은 그대로 둡니다
    /// (기존과 동일하게 처음 Instance에 접근하는 시점에 지연 초기화됩니다).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BootPriorityAttribute : Attribute
    {
        public int Priority { get; }

        public BootPriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }
}
