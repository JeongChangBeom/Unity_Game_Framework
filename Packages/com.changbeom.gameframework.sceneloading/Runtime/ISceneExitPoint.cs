using UnityEngine;

namespace GameFramework.SceneLoading
{
    /// <summary>
    /// 씬에 배치한 컴포넌트가 이 인터페이스를 구현하면, 그 씬을 떠나기 직전(다음 씬
    /// 로드가 시작된 뒤, 실제로 전환되기 전) SceneLoadingManager가 자동으로 찾아서
    /// OnSceneExitAsync()를 호출합니다. 로딩 화면이 이미 화면을 덮은 뒤에 호출되므로,
    /// 여기서 하는 정리 작업이 사용자에게 보이지 않습니다. 등록 절차는 따로 없으며,
    /// 씬 안의 GameObject에 붙이기만 하면 됩니다.
    /// </summary>
    public interface ISceneExitPoint
    {
        Awaitable OnSceneExitAsync();
    }
}
