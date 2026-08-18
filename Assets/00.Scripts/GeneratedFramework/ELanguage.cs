// 자동 생성됨. 직접 편집하지 마세요.

using UnityEngine;
using GameFramework.Localization;

namespace GameFramework.Localization
{
    public enum ELanguage
    {
        None = 0,
        KO,
        EN,
        JP,
    }

    // LocalizationManager는 패키지 쪽 코드라 이 프로젝트 전용 enum을 컴파일
    // 타임에 알 수 없습니다. 대신 언어 코드(string) 기반 API를 감싸는 확장
    // 메서드로 강타입 호출부(lm.SetLanguageAsync(ELanguage.X))를 그대로
    // 제공합니다. OnLanguageChanged는 이벤트라서 확장 메서드로 감쌀 수 없어
    // string 그대로 노출됩니다.
    public static class ELanguageExtensions
    {
        public static Awaitable SetLanguageAsync(this LocalizationManager manager, ELanguage language)
        {
            return manager.SetLanguageAsync(language.ToString());
        }
    }
}
