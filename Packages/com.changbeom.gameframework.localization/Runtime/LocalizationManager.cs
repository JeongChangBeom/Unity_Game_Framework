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
        private Dictionary<ELocKey, Dictionary<ELanguage, string>> _table = new Dictionary<ELocKey, Dictionary<ELanguage, string>>();

        public ELanguage CurrentLanguage { get; private set; }

        public event Action<ELanguage> OnLanguageChanged;

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
        public string GetText(ELocKey key)
        {
            if (!_table.TryGetValue(key, out Dictionary<ELanguage, string> perLanguage))
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
        public Awaitable SetLanguageAsync(ELanguage language)
        {
            CurrentLanguage = language;
            SaveLanguage(language);
            OnLanguageChanged?.Invoke(language);
            return Awaitable.NextFrameAsync();
        }

        private ELanguage DetermineInitialLanguage()
        {
            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(LanguageKey);

            if (SaveManager.Instance.TryLoad(key, out LanguageSaveData saved) && Enum.IsDefined(typeof(ELanguage), saved.Language))
            {
                return saved.Language;
            }

            if (_settings.UseSystemLanguageOnFirstLaunch && TryMapSystemLanguage(out ELanguage systemLanguage))
            {
                return systemLanguage;
            }

            return _settings.DefaultLanguage;
        }

        private void SaveLanguage(ELanguage language)
        {
            SaveKey key = SaveManager.Instance.Domain(SettingsDomain).Join(LanguageKey);
            SaveManager.Instance.Save(key, new LanguageSaveData { Language = language });
            SaveManager.Instance.Flush();
        }

        private static bool TryMapSystemLanguage(out ELanguage language)
        {
            if (SystemLanguageToCode.TryGetValue(Application.systemLanguage, out string code) &&
                Enum.TryParse(code, out language) &&
                Enum.IsDefined(typeof(ELanguage), language))
            {
                return true;
            }

            language = default;
            return false;
        }

        // Data Parsing이 생성한 LocalizationTable을 리플렉션으로 읽습니다 (SoundManager의
        // LoadSoundData와 동일한 패턴). ELanguage의 각 멤버 이름이 테이블 행의 필드
        // 이름과 정확히 같아야 합니다 - LocalizationGenerator가 그렇게 되도록 시트의
        // 언어 컬럼명 그대로 ELanguage를 생성합니다.
        private static Dictionary<ELocKey, Dictionary<ELanguage, string>> LoadLocalizationData(string resourcePath)
        {
            Dictionary<ELocKey, Dictionary<ELanguage, string>> data = new Dictionary<ELocKey, Dictionary<ELanguage, string>>();

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

            string[] languageNames = Enum.GetNames(typeof(ELanguage));

            foreach (object row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                Type rowType = row.GetType();

                string keyName = GetFieldValue<string>(rowType, row, "KeyName");
                if (string.IsNullOrEmpty(keyName))
                {
                    continue;
                }

                if (!Enum.TryParse(keyName, out ELocKey locKey) || !Enum.IsDefined(typeof(ELocKey), locKey))
                {
                    Debug.LogWarning($"[LocalizationManager] KeyName \"{keyName}\"에 해당하는 ELocKey 멤버가 없습니다. Game Framework/Localization/Generate ELocKey + ELanguage From Localization Table로 다시 생성해보세요.");
                    continue;
                }

                Dictionary<ELanguage, string> perLanguage = new Dictionary<ELanguage, string>();

                for (int i = 0; i < languageNames.Length; i++)
                {
                    string languageName = languageNames[i];
                    if (languageName == "None")
                    {
                        continue;
                    }

                    string text = GetFieldValue<string>(rowType, row, languageName);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    Enum.TryParse(languageName, out ELanguage language);
                    perLanguage[language] = text;
                }

                if (data.ContainsKey(locKey))
                {
                    Debug.LogWarning($"[LocalizationManager] KeyName \"{keyName}\"이(가) 테이블에 중복으로 있어 나중 행으로 덮어씁니다.");
                }

                data[locKey] = perLanguage;
            }

            return data;
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
            public ELanguage Language;
        }
    }
}
