using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public static class Lang
    {
        public enum Code
        {
            CN,
            EN
        }

        private static Dictionary<Code, string> _langName = new()
        {
            {Code.CN, "简体中文"},
            {Code.EN, "English"}
        };

        public static string GetLangName(Code code)
        {
            return _langName[code];
        }
        
        private static Settings _settings;
        
        public static Code GetLangCode()
        {
            _settings ??= new Settings("lang");
            var code = _settings.GetString("code", Config.Instance.LangCode);
            return Enum.Parse<Code>(code);
        }

        public static void SetLangCode(Code code)
        {
            _settings ??= new Settings("lang");
            _settings.SetString("code", Enum.GetName(typeof(Code), code));
            _settings.Save();
        }

        public static class Strings
        {
            private static Dictionary<string, string> _strings;
            
            public static async UniTask LoadStrings()
            {
                var jsonPath = $"lang/{GetLangCode()}.json";
                if (!FileUtility.FileExists(FileUtility.FileType.StreamingAssets, jsonPath))
                {
                    Debug.LogError($"Language file not found: {jsonPath}");
                    return;
                }

                try
                {
                    var jsonText = await FileUtility.ReadAllTextAsync(FileUtility.FileType.StreamingAssets, jsonPath);
                    _strings = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load language file: {ex.Message}");
                }
            }

            public static string Get(string key)
            {
                if (_strings != null && _strings.TryGetValue(key, out var value))
                {
                    return value;
                }

                Debug.LogWarning($"Language string not found: {key}");
                return key;
            }
        }
    }
}