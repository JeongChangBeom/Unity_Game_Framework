using System;
using GameFramework.EventBus;
using UnityEngine;

namespace GameFramework.Tests
{
    public readonly struct EnemyDefeatedEvent
    {
        public readonly string EnemyName;
        public readonly int Reward;

        public EnemyDefeatedEvent(string enemyName, int reward)
        {
            EnemyName = enemyName;
            Reward = reward;
        }
    }

    public sealed class EventBusTester : MonoBehaviour
    {
        private bool _soundSubscribed;
        private bool _buggySubscribed;

        private string _log = "";
        private Vector2 _scroll;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("EventBus Tester");

            GUILayout.Label("발행");
            if (GUILayout.Button("1) EnemyDefeatedEvent 발행"))
            {
                EventBus<EnemyDefeatedEvent>.Publish(new EnemyDefeatedEvent("Goblin", 10));
                Log("Publish(EnemyDefeatedEvent)");
            }

            GUILayout.Space(10);
            GUILayout.Label("구독 (사운드 재생 흉내)");
            if (GUILayout.Button(_soundSubscribed ? "2) 구독 해제" : "2) 구독"))
            {
                if (_soundSubscribed)
                {
                    EventBus<EnemyDefeatedEvent>.Unsubscribe(HandleSound);
                }
                else
                {
                    EventBus<EnemyDefeatedEvent>.Subscribe(HandleSound);
                }

                _soundSubscribed = !_soundSubscribed;
            }

            GUILayout.Space(10);
            GUILayout.Label("SubscribeOnce (튜토리얼 팝업 흉내 - 한 번만 반응)");
            if (GUILayout.Button("3) SubscribeOnce로 구독"))
            {
                EventBus<EnemyDefeatedEvent>.SubscribeOnce(HandleTutorialOnce);
                Log("SubscribeOnce(HandleTutorialOnce) -- 다음 발행 한 번만 반응하고 자동 해제됨");
            }

            GUILayout.Space(10);
            GUILayout.Label("예외를 던지는 구독자 (다른 구독자가 계속 실행되는지 확인용)");
            if (GUILayout.Button(_buggySubscribed ? "4) 버그 구독자 해제" : "4) 버그 구독자 구독"))
            {
                if (_buggySubscribed)
                {
                    EventBus<EnemyDefeatedEvent>.Unsubscribe(HandleBuggy);
                }
                else
                {
                    EventBus<EnemyDefeatedEvent>.Subscribe(HandleBuggy);
                }

                _buggySubscribed = !_buggySubscribed;
            }

            GUILayout.Space(10);
            if (GUILayout.Button("5) 전체 구독 해제 (ClearSubscribers)"))
            {
                EventBus<EnemyDefeatedEvent>.ClearSubscribers();
                _soundSubscribed = false;
                _buggySubscribed = false;
                Log("ClearSubscribers()");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Clear Log"))
            {
                _log = "";
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
            GUILayout.TextArea(_log);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void HandleSound(EnemyDefeatedEvent evt)
        {
            Log($"[사운드] {evt.EnemyName} 처치 사운드 재생 (보상 {evt.Reward})");
        }

        private void HandleTutorialOnce(EnemyDefeatedEvent evt)
        {
            Log($"[튜토리얼] {evt.EnemyName} 처치 - 첫 처치 튜토리얼 팝업 (이후로는 다시 안 뜸)");
        }

        private void HandleBuggy(EnemyDefeatedEvent evt)
        {
            Log("[버그 구독자] 예외를 던집니다 -- 아래에 EventBus가 남긴 에러 로그가 보이고, 다른 구독자는 정상 실행되어야 합니다");
            throw new InvalidOperationException("일부러 던진 테스트 예외");
        }

        private void Log(string msg)
        {
            string line = System.DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
