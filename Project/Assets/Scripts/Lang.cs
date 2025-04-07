using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        private static readonly Dictionary<Code, string> _langName = new()
        {
            { Code.CN, "简体中文" },
            { Code.EN, "English" }
        };

        public static string GetLangName(Code code)
        {
            return _langName[code];
        }

        public static class Strings
        {
            private static Dictionary<string, string> _strings;

            public static async UniTask LoadStrings(Code langCode)
            {
                var jsonPath = $"lang/{langCode}.json";
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

            public static string Get(string key, params object[] args)
            {
                if (_strings != null && _strings.TryGetValue(key, out var value))
                {
                    if (args.Length > 0) value = string.Format(value, args);
                    return value;
                }

                Debug.LogWarning($"Language string not found: {key}");
                return key;
            }
        }
    }
}