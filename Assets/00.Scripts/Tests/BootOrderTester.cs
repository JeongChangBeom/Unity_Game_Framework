using System.Collections.Generic;
using GameFramework.Core;
using UnityEngine;

namespace GameFramework.Tests
{
    // Two dummy managers with [BootPriority] to prove they auto-initialize, in order,
    // before the scene even loads -- with zero scene placement. A third manager has no
    // attribute, so it only initializes lazily when something actually touches .Instance,
    // exactly like every manager before this feature existed.

    [BootPriority(-100)]
    public sealed class BootTestManagerA : MonoSingleton<BootTestManagerA>
    {
        protected override void OnInitialize()
        {
            BootOrderLog.Record(nameof(BootTestManagerA));
        }
    }

    [BootPriority(-50)]
    public sealed class BootTestManagerB : MonoSingleton<BootTestManagerB>
    {
        protected override void OnInitialize()
        {
            BootOrderLog.Record(nameof(BootTestManagerB));
        }
    }

    public sealed class BootTestManagerC : MonoSingleton<BootTestManagerC>
    {
        protected override void OnInitialize()
        {
            BootOrderLog.Record(nameof(BootTestManagerC));
        }
    }

    public static class BootOrderLog
    {
        public static readonly List<string> Entries = new List<string>();

        public static void Record(string name)
        {
            Entries.Add(name);
        }
    }

    public sealed class BootOrderTester : MonoBehaviour
    {
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(560, 20, 460, 220));
            GUILayout.Box("Boot Order Tester");

            GUILayout.Label("Expected on Play: A, B already recorded below (auto-booted before scene load).");
            GUILayout.Label("C only appears after you press the button (lazy init, no [BootPriority]).");

            GUILayout.Space(10);
            GUILayout.Label("Recorded order: " + (BootOrderLog.Entries.Count == 0 ? "(empty)" : string.Join(" -> ", BootOrderLog.Entries)));

            GUILayout.Space(10);

            if (GUILayout.Button("Touch BootTestManagerC.Instance (lazy init)"))
            {
                BootTestManagerC touched = BootTestManagerC.Instance;
            }

            GUILayout.EndArea();
        }
    }
}
