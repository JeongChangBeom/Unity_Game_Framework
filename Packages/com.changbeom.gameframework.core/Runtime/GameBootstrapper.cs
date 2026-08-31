using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// [BootPriority]가 붙은 MonoSingleton&lt;T&gt; 타입들을 첫 씬 로드 전에 선언한 순서대로
    /// 초기화합니다. [BootPriority]가 없는 타입은 기존과 동일하게 처음 Instance에
    /// 접근하는 시점에 지연 초기화됩니다.
    /// </summary>
    internal static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            List<(int priority, Type type)> targets = new List<(int, Type)>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null)
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract)
                    {
                        continue;
                    }

                    if (!InheritsMonoSingleton(type))
                    {
                        continue;
                    }

                    BootPriorityAttribute attribute = type.GetCustomAttribute<BootPriorityAttribute>();

                    if (attribute == null)
                    {
                        continue;
                    }

                    targets.Add((attribute.Priority, type));
                }
            }

            targets.Sort((a, b) => a.priority.CompareTo(b.priority));

            foreach ((int _, Type type) in targets)
            {
                PropertyInfo instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                if (instanceProperty == null)
                {
                    continue;
                }

                try
                {
                    instanceProperty.GetValue(null);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameBootstrapper] {type.Name} 부팅 실패: {e}");
                }
            }
        }

        private static bool InheritsMonoSingleton(Type type)
        {
            Type baseType = type.BaseType;

            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(MonoSingleton<>))
                {
                    return true;
                }

                baseType = baseType.BaseType;
            }

            return false;
        }
    }
}
