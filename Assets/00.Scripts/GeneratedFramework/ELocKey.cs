// 자동 생성됨. 직접 편집하지 마세요.

using UnityEngine;
using GameFramework.Localization;

namespace GameFramework.Localization
{
    public enum ELocKey
    {
        None = 0,
        UI_Button_Start,
        UI_Button_Cancel,
    }

    // LocalizationManager는 패키지 쪽 코드라 이 프로젝트 전용 enum을 컴파일
    // 타임에 알 수 없습니다. 대신 KeyName(string) 기반 API를 감싸는 확장
    // 메서드로 강타입 호출부(lm.GetText(ELocKey.X))를 그대로 제공합니다.
    public static class ELocKeyExtensions
    {
        public static string GetText(this LocalizationManager manager, ELocKey key)
        {
            return manager.GetText(key.ToString());
        }
    }
}
