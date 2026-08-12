using GameFramework.FSM;
using UnityEngine;

namespace GameFramework.Tests
{
    public enum ETestState
    {
        Idle,
        Attack,
        Unregistered, // 등록 안 된 상태로 전환 시도하는 에러 케이스 테스트용 (일부러 AddState 안 함)
        Redirect, // OnEnter 안에서 재진입 ChangeState를 호출하는 테스트용
    }

    public sealed class AttackTestState : StateBase
    {
        private bool _canExit;

        public override bool CanExit => _canExit;

        public override void OnEnter()
        {
            _canExit = false;
        }

        /// <summary>공격 애니메이션의 캔슬 가능 프레임에 걸어둔 Animation Event를 흉내냅니다.</summary>
        public void SimulateCancelableFrame()
        {
            _canExit = true;
        }
    }

    /// <summary>OnEnter 안에서 곧바로 다른 상태로 전환을 시도합니다 - StateMachine의
    /// 재진입 방지 가드가 이 요청을 무시하고 경고 로그만 남기는지 확인하는 용도입니다.</summary>
    public sealed class RedirectTestState : StateBase
    {
        private readonly StateMachine<ETestState> _fsm;

        public RedirectTestState(StateMachine<ETestState> fsm)
        {
            _fsm = fsm;
        }

        public override void OnEnter()
        {
            _fsm.ChangeState(ETestState.Idle); // 재진입 - 무시되고 경고만 남아야 함
        }
    }

    public sealed class FSMTester : MonoBehaviour
    {
        private readonly StateMachine<ETestState> _fsm = new StateMachine<ETestState>();
        private readonly AttackTestState _attackState = new AttackTestState();

        private string _log = "";
        private Vector2 _scroll;

        private void Awake()
        {
            _fsm.AddState(ETestState.Idle, new StateBase());
            _fsm.AddState(ETestState.Attack, _attackState);
            _fsm.AddState(ETestState.Redirect, new RedirectTestState(_fsm));
            _fsm.OnStateChanged += HandleStateChanged;

            _fsm.ChangeState(ETestState.Idle);
        }

        private void OnDestroy()
        {
            _fsm.OnStateChanged -= HandleStateChanged;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("FSM Tester");

            GUILayout.Label($"CurrentKey={_fsm.CurrentKey}, CanChangeState={_fsm.CanChangeState}");

            GUILayout.Space(10);
            if (GUILayout.Button("1) Idle로 전환"))
            {
                _fsm.ChangeState(ETestState.Idle);
                Log("ChangeState(Idle)");
            }

            if (GUILayout.Button("2) Attack으로 전환 (진입 직후 CanExit=false)"))
            {
                _fsm.ChangeState(ETestState.Attack);
                Log("ChangeState(Attack)");
            }

            if (GUILayout.Button("3) 애니메이션 이벤트 흉내 (Attack을 캔슬 가능하게)"))
            {
                _attackState.SimulateCancelableFrame();
                Log("AttackTestState.SimulateCancelableFrame() -- 이제 CanChangeState가 true여야 함");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("4) 등록 안 된 상태로 전환 시도 (에러 로그 확인)"))
            {
                _fsm.ChangeState(ETestState.Unregistered);
            }

            GUILayout.Space(10);
            if (GUILayout.Button("5) 재진입 테스트 (OnEnter 안에서 ChangeState 호출 - 경고 로그 + CurrentKey는 Redirect로 유지되어야 함)"))
            {
                _fsm.ChangeState(ETestState.Redirect);
                Log($"ChangeState(Redirect) 이후 CurrentKey={_fsm.CurrentKey} (Redirect여야 정상 - Idle로 안 바뀌어 있어야 함)");
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

        private void HandleStateChanged(ETestState previous, ETestState next)
        {
            Log($"OnStateChanged: {previous} -> {next}");
        }

        private void Log(string msg)
        {
            string line = System.DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
