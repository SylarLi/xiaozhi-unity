using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Lang
{
    public static class Code
    {
        public static readonly string Value = "zh-CN";
    }

    public static class Strings
    {
        private static Dictionary<string, string> _strings;

        static Strings()
        {
            LoadStrings();
        }

        private static void LoadStrings()
        {
            var jsonPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, $"lang", $"{Code.Value}.json");
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"Language file not found: {jsonPath}");
                return;
            }

            try
            {
                var jsonText = File.ReadAllText(jsonPath);
                _strings = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load language file: {ex.Message}");
            }
        }

        public static string ACCESS_VIA_BROWSER => GetString(nameof(ACCESS_VIA_BROWSER));
        public static string ACTIVATION => GetString(nameof(ACTIVATION));
        public static string CONNECTED_TO => GetString(nameof(CONNECTED_TO));
        public static string CONNECTING => GetString(nameof(CONNECTING));
        public static string CONNECT_TO => GetString(nameof(CONNECT_TO));
        public static string CONNECT_TO_HOTSPOT => GetString(nameof(CONNECT_TO_HOTSPOT));
        public static string DETECTING_MODULE => GetString(nameof(DETECTING_MODULE));
        public static string ENTERING_WIFI_CONFIG_MODE => GetString(nameof(ENTERING_WIFI_CONFIG_MODE));
        public static string ERROR => GetString(nameof(ERROR));
        public static string INITIALIZING => GetString(nameof(INITIALIZING));
        public static string LISTENING => GetString(nameof(LISTENING));
        public static string LOADING_PROTOCOL => GetString(nameof(LOADING_PROTOCOL));
        public static string NEW_VERSION => GetString(nameof(NEW_VERSION));
        public static string OTA_UPGRADE => GetString(nameof(OTA_UPGRADE));
        public static string PIN_ERROR => GetString(nameof(PIN_ERROR));
        public static string REGISTERING_NETWORK => GetString(nameof(REGISTERING_NETWORK));
        public static string REG_ERROR => GetString(nameof(REG_ERROR));
        public static string SCANNING_WIFI => GetString(nameof(SCANNING_WIFI));
        public static string SERVER_ERROR => GetString(nameof(SERVER_ERROR));
        public static string SERVER_NOT_CONNECTED => GetString(nameof(SERVER_NOT_CONNECTED));
        public static string SERVER_NOT_FOUND => GetString(nameof(SERVER_NOT_FOUND));
        public static string SERVER_TIMEOUT => GetString(nameof(SERVER_TIMEOUT));
        public static string SPEAKING => GetString(nameof(SPEAKING));
        public static string STANDBY => GetString(nameof(STANDBY));
        public static string UPGRADE_FAILED => GetString(nameof(UPGRADE_FAILED));
        public static string UPGRADING => GetString(nameof(UPGRADING));
        public static string VERSION => GetString(nameof(VERSION));
        public static string WIFI_CONFIG_MODE => GetString(nameof(WIFI_CONFIG_MODE));

        private static string GetString(string key)
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