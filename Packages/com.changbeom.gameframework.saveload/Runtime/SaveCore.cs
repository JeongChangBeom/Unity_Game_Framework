using System;

namespace GameFramework.SaveLoad
{
    /// <summary>
    /// 순수 C# 오케스트레이션 계층입니다: 현재 활성화된 ISaveProvider 위에서 RootKey
    /// 네임스페이싱, dirty 추적, SaveMeta 관리, 선택적 백업/복구를 처리합니다.
    /// Unity의 MonoBehaviour 생명주기와 무관하게 테스트할 수 있습니다.
    /// </summary>
    public sealed class SaveCore
    {
        private readonly ISaveProvider _provider;
        private readonly string _rootKey;

        private bool _dirty;

        public bool IsDirty => _dirty;

        public SaveCore(ISaveProvider provider, string rootKey)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _rootKey = rootKey ?? string.Empty;
        }

        public void Save<T>(string key, T value)
        {
            _provider.Set(WithRoot(key), value);
            _dirty = true;
        }

        public bool TryLoad<T>(string key, out T value)
        {
            return _provider.TryGet(WithRoot(key), out value);
        }

        public T LoadOrCreate<T>(string key, Func<T> factory, bool saveIfMissing)
        {
            if (TryLoad(key, out T value))
            {
                return value;
            }

            value = factory();

            if (saveIfMissing)
            {
                Save(key, value);
            }

            return value;
        }

        public bool HasKey(string key)
        {
            return _provider.HasKey(WithRoot(key));
        }

        public void Delete(string key)
        {
            _provider.DeleteKey(WithRoot(key));
            _dirty = true;
        }

        /// <summary>실제로 영속 저장소에 쓰기까지 성공했으면 true입니다. false면 dirty 상태가 유지되어 다음 Flush에서 재시도됩니다.</summary>
        public bool Flush()
        {
            if (!_dirty)
            {
                return true;
            }

            _provider.Set(WithRoot(SaveMeta.LastSavedAtUtc), DateTime.UtcNow.Ticks);

            bool ok = _provider.Flush();
            if (ok)
            {
                _dirty = false;
            }

            return ok;
        }

        public void EnsureMeta(int currentVersion)
        {
            if (!HasKey(SaveMeta.CreatedAtUtc))
            {
                Save(SaveMeta.CreatedAtUtc, DateTime.UtcNow.Ticks);
            }

            int storedVersion = LoadOrCreate(SaveMeta.SaveVersion, () => currentVersion, saveIfMissing: true);

            if (storedVersion != currentVersion)
            {
                Save(SaveMeta.SaveVersion, currentVersion);
            }

            Flush();
        }

        public bool HasBackup()
        {
            return _provider is ISaveBackupProvider backup && backup.HasBackup();
        }

        public bool BackupNow()
        {
            if (_provider is not ISaveBackupProvider backup)
            {
                return false;
            }

            bool ok = backup.BackupNow();

            // BackupNow는 Provider 내부에서 자체적으로 Flush까지 수행하므로, 성공했다면
            // SaveCore가 들고 있는 dirty 플래그도 함께 내려줘야 IsDirty가 실제 디스크
            // 상태와 어긋나지 않습니다 (Provider의 Flush()는 SaveCore.Flush()를 거치지
            // 않고 직접 호출되어 이 플래그를 모릅니다).
            if (ok)
            {
                _dirty = false;
            }

            return ok;
        }

        public bool RestoreFromBackup()
        {
            if (_provider is not ISaveBackupProvider backup)
            {
                return false;
            }

            bool ok = backup.RestoreFromBackup();

            // RestoreFromBackup은 in-memory 데이터를 백업 시점 상태로 통째로 되돌리므로,
            // 그 전까지 쌓여 있던 미반영 변경 사항은 더 이상 유효하지 않습니다. dirty를
            // 그대로 두면 다음 Flush가 이미 되돌려진 데이터 위에 옛 상태를 다시 써버릴
            // 여지가 생기므로 여기서 같이 내려줍니다.
            if (ok)
            {
                _dirty = false;
            }

            return ok;
        }

        // 공개 API가 SaveKey가 아니라 string을 받기 때문에, 호출자가 SaveKey 생성자의
        // 검증/정규화(null/공백 거부, 백슬래시 통일, 중복 슬래시 정리)를 그냥 건너뛰고
        // 다듬어지지 않은 문자열을 바로 넘길 수 있었습니다. 여기서 SaveKey를 한 번 거치게
        // 해서, SaveKey를 거쳐 온 값이든 리터럴 문자열이든 항상 같은 규칙이 적용되도록 합니다.
        private string WithRoot(string key)
        {
            string normalized = new SaveKey(key).Value;
            return string.IsNullOrEmpty(_rootKey) ? normalized : $"{_rootKey}/{normalized}";
        }
    }
}
