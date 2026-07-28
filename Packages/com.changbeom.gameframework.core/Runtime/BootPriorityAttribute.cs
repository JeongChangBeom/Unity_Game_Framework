using System;

namespace GameFramework.Core
{
    /// <summary>
    /// Declares an explicit, deterministic initialization order for a MonoSingleton-derived
    /// manager. Lower values initialize first. Types without this attribute are left alone
    /// (they still lazily initialize on first Instance access, exactly as before).
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
