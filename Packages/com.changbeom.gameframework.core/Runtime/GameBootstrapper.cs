using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// Scans loaded assemblies for MonoSingleton&lt;T&gt; types marked with [BootPriority],
    /// sorts them, and triggers each one's static Instance getter in that order before the
    /// first scene loads. This lets a manager's declared order win over Unity's undefined
    /// Awake() ordering across root GameObjects, without requiring any scene placement --
    /// MonoSingleton's existing "auto-create on first access" behavior does the rest.
    ///
    /// Types without [BootPriority] are untouched: they keep initializing lazily on first
    /// Instance access, exactly as before this existed.
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
                PropertyInfo instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

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
                    Debug.LogError($"[GameBootstrapper] Failed to boot {type.Name}: {e}");
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
