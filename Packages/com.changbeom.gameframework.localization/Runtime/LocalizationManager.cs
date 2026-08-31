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
    /// 번역 키와 언어 코드는 전부 문자열로 다룹니다 - 강타입으로 쓰고 싶으면
    /// `LocalizationTable.Get(ELocKey key)`로 행을 조회해 Key를 얻으세요. 언어가 바뀔
    /// 때마다 OnLanguageChanged가 발행되므로, 음성 더빙/언어별 이미지 등도 이 이벤트만
    /// 구독하면 됩니다.
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

                if (languageFieldNames == null)
                {
                    languageFieldNames = ExtractLanguageFieldNames(rowType);
                }

                string key = GetFieldValue<string>(rowType, row, "Key");
                if (string.IsNullOrEmpty(key))
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

                if (data.ContainsKey(key))
                {
                    Debug.LogWarning($"[LocalizationManager] Key \"{key}\"이(가) 테이블에 중복으로 있어 나중 행으로 덮어씁니다.");
                }

                data[key] = perLanguage;
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

                if (field.FieldType != typeof(string) || field.Name == "Key")
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
