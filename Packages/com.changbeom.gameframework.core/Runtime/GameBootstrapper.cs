using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 로드된 모든 어셈블리에서 [BootPriority]가 붙은 MonoSingleton&lt;T&gt; 타입을 찾아
    /// 정렬한 뒤, 첫 씬이 로드되기 전에 그 순서대로 각 타입의 static Instance 게터를 호출합니다.
    /// 이렇게 하면 씬 배치 없이도, 루트 GameObject들의 정의되지 않은 Awake() 호출 순서보다
    /// 매니저가 선언한 순서가 우선하게 됩니다 -- 나머지는 MonoSingleton의 기존
    /// "처음 접근 시 자동 생성" 동작이 처리합니다.
    ///
    /// [BootPriority]가 없는 타입은 건드리지 않습니다: 이 기능이 없었을 때와 마찬가지로
    /// 처음 Instance에 접근하는 시점에 지연 초기화됩니다.
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
