using GameFramework.Utility;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class ReactiveTester : MonoBehaviour
    {
        private const int MaxHp = 100;

        private readonly ReactiveProperty<int> _hp = new ReactiveProperty<int>(MaxHp);

        private int _lastNotifiedValue;
        private bool _subscribed;

        private string _log = "";
        private Vector2 _scroll;

        private void OnEnable()
        {
            _hp.Subscribe(HandleHpChanged);
            _subscribed = true;
        }

        private void OnDisable()
        {
            _hp.Unsubscribe(HandleHpChanged);
            _subscribed = false;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("Reactive Tester");

            GUILayout.Label($"HP.Value={_hp.Value} / {MaxHp}, 구독 여부={_subscribed}, 마지막 알림 값={_lastNotifiedValue}");

            GUILayout.Space(10);
            if (GUILayout.Button("1) 데미지 -10"))
            {
                _hp.Value = Mathf.Max(0, _hp.Value - 10);
                Log($"Value -= 10 -> {_hp.Value}");
            }

            if (GUILayout.Button("2) 힐 +10"))
            {
                _hp.Value = Mathf.Min(MaxHp, _hp.Value + 10);
                Log($"Value += 10 -> {_hp.Value}");
            }

            if (GUILayout.Button("3) 같은 값으로 다시 세팅 (알림 안 와야 함)"))
            {
                _hp.Value = _hp.Value;
                Log($"Value = {_hp.Value} (동일 값 재대입)");
            }

            GUILayout.Space(10);
            if (GUILayout.Button(_subscribed ? "4) 구독 해제" : "4) 다시 구독 (구독 시 현재 값 즉시 수신)"))
            {
                if (_subscribed)
                {
                    _hp.Unsubscribe(HandleHpChanged);
                }
                else
                {
                    _hp.Subscribe(HandleHpChanged);
                }

                _subscribed = !_subscribed;
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Clear Log"))
            {
                _log = "";
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
            GUILayout.TextArea(_log);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void HandleHpChanged(int value)
        {
            _lastNotifiedValue = value;
            Log($"OnValueChanged: {value}");
        }

        private void Log(string msg)
        {
            string line = System.DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
