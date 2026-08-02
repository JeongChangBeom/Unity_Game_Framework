using System.Collections.Generic;
using GameFramework.Core;
using UnityEngine;

namespace GameFramework.Tests
{
    // [BootPriority]가 붙은 더미 매니저 2개는 씬 배치 없이도 씬이 로드되기 전에
    // 순서대로 자동 초기화됨을 보여줍니다. 세 번째 매니저는 attribute가 없어서,
    // 이 기능이 없던 때와 마찬가지로 누군가 실제로 .Instance를 건드릴 때만 지연 초기화됩니다.

    [BootPriority(1)]
    public sealed class BootTestManagerA : MonoSingleton<BootTestManagerA>
    {
        protected override void OnInitialize()
        {
            BootOrderLog.Record(nameof(BootTestManagerA));
        }
    }

    [BootPriority(2)]
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

            GUILayout.Label("Play 시 기대 동작: A, B는 씬 로드 전에 이미 자동 초기화되어 아래에 기록됨.");
            GUILayout.Label("C는 버튼을 눌러야만 나타남 (지연 초기화, [BootPriority] 없음).");

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
