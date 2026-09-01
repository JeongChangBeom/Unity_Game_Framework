namespace GameFramework.SaveLoad
{
    public interface ISaveProvider
    {
        bool HasKey(string key);
        void DeleteKey(string key);
        void Set<T>(string key, T value);
        bool TryGet<T>(string key, out T value);

        /// <summary>실제로 영속 저장소에 쓰기까지 성공했으면 true입니다. 호출자는 false일 때 dirty 상태를 유지해 다음 기회에 재시도해야 합니다.</summary>
        bool Flush();

        /// <summary>이 provider가 관리하는 모든 데이터를 삭제합니다 (테스트/QA용 초기화).</summary>
        void DeleteAll();
    }
}
