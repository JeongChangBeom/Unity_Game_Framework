// 자동 생성됨. 직접 편집하지 마세요.

using UnityEngine;
using GameFramework.SoundSystem;

namespace GameFramework.SoundSystem
{
    public enum ESound
    {
        None = 0,
        BGM_Test,
        SFX_Test,
        BGM_Dummy,
    }

    // SoundManager는 패키지 쪽 코드라 이 프로젝트 전용 enum을 컴파일 타임에
    // 알 수 없습니다. 대신 FileName(string) 기반 API를 감싸는 확장 메서드로
    // 강타입 호출부(sm.PlaySound(ESound.X))를 그대로 제공합니다.
    public static class ESoundExtensions
    {
        public static void PlaySound(this SoundManager manager, ESound id)
        {
            if (id == ESound.None)
            {
                return;
            }

            manager.PlaySound(id.ToString());
        }

        public static void StopSound(this SoundManager manager, ESound id)
        {
            if (id == ESound.None)
            {
                return;
            }

            manager.StopSound(id.ToString());
        }
    }
}
