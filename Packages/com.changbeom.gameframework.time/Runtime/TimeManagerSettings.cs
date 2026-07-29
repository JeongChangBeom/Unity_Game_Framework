using System;
using UnityEngine;

namespace GameFramework.TimeSystem
{
    /// <summary>
    /// 프로젝트별 TimeManager 설정입니다. Assets/Create/Game Framework/Time System/Time
    /// Manager Settings로 생성한 뒤 Assets/Resources/GameFramework/TimeManagerSettings.asset
    /// 경로에 두면, 씬 배치 없이도 TimeManager가 찾을 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TimeManagerSettings",
        menuName = "Game Framework/Time System/Time Manager Settings")]
    public sealed class TimeManagerSettings : ScriptableObject
    {
        public const string ResourcePath = "GameFramework/TimeManagerSettings";

        [Header("Time Source")]
        [Tooltip("LocalOnly: 기기 시간만 사용. ServerOnly: 서버 동기화 시간만 사용(미신뢰 시 기기 시간으로 대체). PreferServer: 서버 동기화가 유효하면 서버, 아니면 기기 시간.")]
        public TimeMode Mode = TimeMode.PreferServer;

        [Header("Reset")]
        [Range(0, 23)] public int DailyResetHour = 0;
        public DayOfWeek WeeklyResetDay = DayOfWeek.Monday;

        [Header("Trust / Cheat Detection")]
        [Tooltip("기기 시간이 이만큼(초) 이상 뒤로 가면 치트로 간주 (미신뢰 소스일 때만 적용되는 허용 오차)")]
        public int BackwardToleranceSeconds = 120;

        [Tooltip("서버 동기화 후 이 시간(초)이 지나면 서버 시간을 더 이상 신뢰하지 않음")]
        public int ServerTrustWindowSeconds = 24 * 60 * 60;

        [Header("Schema")]
        public int SchemaVersion = 1;
    }
}
