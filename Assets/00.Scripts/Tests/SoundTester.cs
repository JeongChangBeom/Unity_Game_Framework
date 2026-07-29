using GameFramework.SoundSystem;
using UnityEngine;

namespace GameFramework.Tests
{
    public sealed class SoundTester : MonoBehaviour
    {
        [SerializeField] private ESound _bgmSound = ESound.BGM_Test;
        [SerializeField] private ESound _oneShotSound = ESound.SFX_Test;

        private string _log = "";
        private Vector2 _scroll;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 560, Screen.height - 40));
            GUILayout.Box("Sound Tester");

            GUILayout.Label("Playback");
            if (GUILayout.Button("1) Play BGM"))
            {
                SoundManager.Instance.PlaySound(_bgmSound);
                Log("PlaySound(" + _bgmSound + ")");
            }

            if (GUILayout.Button("2) Stop BGM"))
            {
                SoundManager.Instance.StopBgm();
                Log("StopBgm()");
            }

            if (GUILayout.Button("3) Play One-Shot"))
            {
                SoundManager.Instance.PlaySound(_oneShotSound);
                Log("PlaySound(" + _oneShotSound + ")");
            }

            if (GUILayout.Button("4) Stop This Sound via StopSound(BGM id) -- proves StopSound also stops BGM"))
            {
                SoundManager.Instance.StopSound(_bgmSound);
                Log("StopSound(" + _bgmSound + ")");
            }

            if (GUILayout.Button("5) Stop All One-Shots"))
            {
                SoundManager.Instance.StopAllOneShots();
                Log("StopAllOneShots()");
            }

            if (GUILayout.Button("6) Stop All Sounds (BGM + One-Shots)"))
            {
                SoundManager.Instance.StopAll();
                Log("StopAll()");
            }

            GUILayout.Space(10);
            GUILayout.Label("Volume");
            if (GUILayout.Button("7) Master 0.5"))
            {
                SoundManager.Instance.SetMasterVolume(0.5f);
                Log("MasterVolume=0.5");
            }

            if (GUILayout.Button("8) Master 1.0"))
            {
                SoundManager.Instance.SetMasterVolume(1f);
                Log("MasterVolume=1.0");
            }

            if (GUILayout.Button("9) BGM Channel 0.3"))
            {
                SoundManager.Instance.SetChannelVolume(ESoundChannel.BGM, 0.3f);
                Log("BGM Channel Volume=0.3");
            }

            if (GUILayout.Button("10) Print Current Volumes"))
            {
                SoundManager sm = SoundManager.Instance;
                Log("Master=" + sm.GetMasterVolume()
                    + " BGM=" + sm.GetChannelVolume(ESoundChannel.BGM)
                    + " SFX=" + sm.GetChannelVolume(ESoundChannel.SFX)
                    + " UI=" + sm.GetChannelVolume(ESoundChannel.UI)
                    + " Voice=" + sm.GetChannelVolume(ESoundChannel.Voice));
            }

            GUILayout.Space(10);
            GUILayout.Label("Ducking (play a Voice-channel sound to test)");
            if (GUILayout.Button("11) Play BGM then simulate Voice ducking"))
            {
                SoundManager.Instance.PlaySound(_bgmSound);
                Log("BGM 시작됨. 덕킹을 확인하려면 Voice 채널 ESound를 재생하세요.");
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

        private void Log(string msg)
        {
            string line = System.DateTime.Now.ToString("HH:mm:ss") + " | " + msg;
            Debug.Log(line);
            _log = string.IsNullOrEmpty(_log) ? line : _log + "\n" + line;
        }
    }
}
