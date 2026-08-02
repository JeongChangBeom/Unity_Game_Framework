using UnityEngine;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 씬에 배치한 컴포넌트가 이 인터페이스를 구현하면, 그 씬이 로드되어 활성화된 직후
    /// SceneLoadingManager가 자동으로 찾아서 OnSceneEnterAsync()를 호출합니다. 로딩
    /// 화면은 모든 ISceneEntryPoint의 OnSceneEnterAsync()가 끝날 때까지 유지되므로,
    /// 플레이어 스폰이나 씬 전용 데이터 로드처럼 "씬이 실제로 준비된 뒤에" 화면이
    /// 사라져야 하는 작업을 여기서 처리하세요. 등록 절차는 따로 없으며, 씬 안의
    /// GameObject에 붙이기만 하면 됩니다.
    /// </summary>
    public interface ISceneEntryPoint
    {
        /// <summary>한 씬에 여러 개가 있을 때 실행 순서입니다. 숫자가 작을수록 먼저 실행됩니다.</summary>
        int Order => 0;

        Awaitable OnSceneEnterAsync();
    }
}
