using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace XiaoZhi.Unity.Lang
{
    public static class Strings
    {
        private static Dictionary<string, string> _strings;

        public static async UniTask LoadStrings()
        {
            var jsonPath = $"lang/{Config.Instance.LangCode}.json";
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