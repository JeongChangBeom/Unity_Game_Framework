using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameFramework.Core;
using GameFramework.SaveLoad;
using UnityEngine;

namespace GameFramework.Localization
{
    /// <summary>
    /// Data Parsing이 생성한 Localization 테이블을 읽어 텍스트를 제공하는 매니저입니다.
    /// 패키지가 프로젝트가 생성한 LocalizationTable 타입을 직접 참조할 수 없기 때문에
    /// (Sound의 SoundManager와 동일한 이유), 부팅 시 리플렉션으로 1회 읽어 자체
    /// Dictionary로 캐싱합니다.
    ///
    /// 번역 키와 언어 코드는 전부 문자열입니다 - 예전에는 이 패키지 자신의 Runtime 폴더
    /// 안에 재생성되는 ELocKey/ELanguage enum이었지만, git URL로 설치된 패키지는 읽기
    /// 전용 캐시에서 로드되어 그 방식이 성립하지 않습니다(SceneLoading의 ESceneKey와
    /// 동일한 이유). 강타입 호출부(GetText(ELocKey.X), SetLanguageAsync(ELanguage.X))는
    /// 프로젝트 쪽에 생성되는 ELocKeyExtensions 확장 메서드가 담당합니다. 다만
    /// OnLanguageChanged는 이벤트라서(확장 메서드로 감쌀 수 없음) string 그대로 노출됩니다.
    ///
    /// 언어가 바뀔 때마다 OnLanguageChanged가 발행되므로, 음성 더빙/언어별 이미지처럼
    /// 나중에 추가될 수 있는 기능도 이 이벤트만 구독하면 됩니다 - LocalizationManager
    /// 자체를 다시 고칠 필요가 없습니다.
    /// </summary>
    public sealed class LocalizationManager : MonoSingleton<LocalizationManager>
    {
        private const string SettingsDomain = "settings";
        private const string LanguageKey = "language";

        private static readonly Dictionary<SystemLanguage, string> SystemLanguageToCode = new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.Korean, "KO" },
            { SystemLanguage.English, "EN" },
            { SystemLanguage.Japanese, "JP" },
            { SystemLanguage.ChineseSimplified, "ZH" },
            { SystemLanguage.ChineseTraditional, "ZH" },
            { SystemLanguage.German, "DE" },
            { SystemLanguage.French, "FR" },
            { SystemLanguage.Spanish, "ES" },
        };

        private LocalizationManagerSettings _settings;
        private Dictionary<string, Dictionary<string, string>> _table = new Dictionary<string, Dictionary<string, string>>();

        public string CurrentLanguage { get; private set; }

        public event Action<string> OnLanguageChanged;

        protected override void OnInitialize()
        {
            _settings = LoadSettings();
            _table = LoadLocalizationData(_settings.TableResourcePath);
            CurrentLanguage = DetermineInitialLanguage();
        }

        private static LocalizationManagerSettings LoadSettings()
        {
            LocalizationManagerSettings settings = Resources.Load<LocalizationManagerSettings>(LocalizationManagerSettings.ResourcePath);

            if (settings != null)
            {
                return settings;
            }

            Debug.LogWarning($"[LocalizationManager] Resources/{LocalizationManagerSettings.ResourcePath}에서 LocalizationManagerSettings 에셋을 찾지 못했습니다. 기본값을 사용합니다. Assets/Create/Game Framework/Localization/Localization Manager Settings로 에셋을 만드세요.");
            return ScriptableObject.CreateInstance<LocalizationManagerSettings>();
        }

        /// <summary>키의 현재 언어 텍스트를 반환합니다. 현재 언어에 번역이 없으면 Fallback
        /// Language를 시도하고, 그마저 없으면 "[MISSING:key]"를 반환하며 에러 로그를 남깁니다.</summary>
        public string GetText(string key)
        {
            if (!_table.TryGetValue(key, out Dictionary<string, string> perLanguage))
            {
                Debug.LogError($"[LocalizationManager] {key} 키가 테이블에 없습니다.");
                return $"[MISSING:{key}]";
            }

            if (perLanguage.TryGetValue(CurrentLanguage, out string text) && !string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (perLanguage.TryGetValue(_settings.FallbackLanguage, out string fallback) && !string.IsNullOrEmpty(fallback))
            {
                Debug.LogWarning($"[LocalizationManager] {key}의 {CurrentLanguage} 번역이 없어 {_settings.FallbackLanguage}로 대체합니다.");
                return fallback;
            }

            Debug.LogError($"[LocalizationManager] {key}의 {CurrentLanguage}/{_settings.FallbackLanguage} 번역이 모두 없습니다.");
            return $"[MISSING:{key}]";
        }

        /// <summary>언어를 바꾸고 저장합니다. v1은 즉시 완료되는 동기 작업이지만, 나중에
        /// 언어별 리소스(음성 등)를 비동기로 로드해야 해도 호출부를 바꿀 필요가 없도록
        /// 처음부터 Awaitable을 반환합니다.</summary>
        public Awaitable SetLanguageAsync(string language)
        {
            CurrentLanguage = language;
            SaveLanguage(language);
            SafeInvokeLanguageChanged(language);
            return Awaitable.NextFrameAsync();
        }

        // EventBus.Publish와 동일한 패턴입니다: 구독자 하나(예: 파괴된 컴포넌트를 참조하는
        // 핸들러)가 예외를 던져도 나머지 구독자에게는 정상적으로 전달되도록 각각 개별
        // try/catch로 격리합니다. 격리가 없으면 한 구독자의 예외로 그 뒤 구독자들이
        // 언어 변경 알림을 통째로 못 받아 일부 UI만 예전 언어로 남는 문제가 있었습니다.
        private void SafeInvokeLanguageChanged(string language)
        {
            if (OnLanguageChanged == null)
            {
                return;
            }

            Delegate[] handlers = OnLanguageChanged.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<string>)handlers[i]).Invoke(language);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LocalizationManager] OnLanguageChanged 구독자에서 예외가 발생했습니다: {e}");
                }
            }
        }

        private string DetermineInitialLanguage()
        {
            // LocalizationManager는 BootPriority 없이 지연 초기화되므로, 이 첫 접근이
            // 다른 매니저의 종료 처리 도중(예: 어떤 오브젝트의 OnDestroy가 이 시점에
            // 처음으로 GetText를 호출) 일어나면 SaveManager는 이미 종료되어
            // Instance가 null일 수 있습니다.
            if (SaveManager.Instance == null)
            {
                return _settings.DefaultLanguage;
            }

            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(LanguageKey);

            if (SaveManager.Instance.TryLoad(key, out LanguageSaveData saved) && !string.IsNullOrEmpty(saved.Language))
            {
                return saved.Language;
            }

            if (_settings.UseSystemLanguageOnFirstLaunch && TryMapSystemLanguage(out string systemLanguage))
            {
                return systemLanguage;
            }

            return _settings.DefaultLanguage;
        }

        private void SaveLanguage(string language)
        {
            // 위 DetermineInitialLanguage와 동일한 이유의 방어입니다.
            if (SaveManager.Instance == null)
            {
                return;
            }

            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(LanguageKey);
            SaveManager.Instance.Save(key, new LanguageSaveData { Language = language });
            SaveManager.Instance.Flush();
        }

        private static bool TryMapSystemLanguage(out string language)
        {
            if (SystemLanguageToCode.TryGetValue(Application.systemLanguage, out string code))
            {
                language = code;
                return true;
            }

            language = null;
            return false;
        }

        // Data Parsing이 생성한 LocalizationTable을 리플렉션으로 읽습니다 (SoundManager의
        // LoadSoundData와 동일한 패턴). 언어 컬럼은 RowKey(int)/KeyName(언어 아님)을 제외한
        // string 타입 필드를 전부 언어 컬럼으로 간주해서 찾습니다 - LocalizationGenerator.
        // ExtractLanguageColumns(에디터, SerializedProperty 기반)와 동일한 판별 기준을
        // 런타임 리플렉션으로 재현한 것이라, 언어 컬럼 집합이 항상 실제 테이블과
        // 자체적으로 일치합니다(더 이상 별도 enum과 동기화될 필요가 없음).
        private static Dictionary<string, Dictionary<string, string>> LoadLocalizationData(string resourcePath)
        {
            Dictionary<string, Dictionary<string, string>> data = new Dictionary<string, Dictionary<string, string>>();

            ScriptableObject table = Resources.Load<ScriptableObject>(resourcePath);
            if (table == null)
            {
                Debug.LogError($"[LocalizationManager] Resources/{resourcePath}에서 Localization 테이블을 찾지 못했습니다. Data Parsing으로 Localization 시트를 생성했는지 확인하세요. 생성 전까지 GetText는 전부 [MISSING]을 반환합니다.");
                return data;
            }

            Type tableType = table.GetType();
            PropertyInfo tableProp = tableType.GetProperty("Table", BindingFlags.Public | BindingFlags.Instance);

            if (tableProp == null || !(tableProp.GetValue(table) is IEnumerable rows))
            {
                Debug.LogError($"[LocalizationManager] {tableType.Name}에서 Table 프로퍼티를 찾지 못했습니다.");
                return data;
            }

            string[] languageFieldNames = null;

            foreach (object row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                Type rowType = row.GetType();

                // 모든 행이 같은 필드 구조를 공유하므로 첫 번째 행에서 한 번만 찾습니다.
                if (languageFieldNames == null)
                {
                    languageFieldNames = ExtractLanguageFieldNames(rowType);
                }

                string keyName = GetFieldValue<string>(rowType, row, "KeyName");
                if (string.IsNullOrEmpty(keyName))
                {
                    continue;
                }

                Dictionary<string, string> perLanguage = new Dictionary<string, string>();

                for (int i = 0; i < languageFieldNames.Length; i++)
                {
                    string languageName = languageFieldNames[i];
                    string text = GetFieldValue<string>(rowType, row, languageName);

                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    perLanguage[languageName] = text;
                }

                if (data.ContainsKey(keyName))
                {
                    Debug.LogWarning($"[LocalizationManager] KeyName \"{keyName}\"이(가) 테이블에 중복으로 있어 나중 행으로 덮어씁니다.");
                }

                data[keyName] = perLanguage;
            }

            return data;
        }

        private static string[] ExtractLanguageFieldNames(Type rowType)
        {
            FieldInfo[] fields = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            List<string> names = new List<string>();

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];

                if (field.FieldType != typeof(string) || field.Name == "KeyName")
                {
                    continue;
                }

                names.Add(field.Name);
            }

            return names.ToArray();
        }

        private static T GetFieldValue<T>(Type type, object instance, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

            if (field == null || !(field.GetValue(instance) is T value))
            {
                return default;
            }

            return value;
        }

        [Serializable]
        private sealed class LanguageSaveData
        {
            public string Language;
        }
    }
}
