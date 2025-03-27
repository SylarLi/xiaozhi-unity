using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class Config
    {
        private static Config _instance;
        public static Config Instance => _instance ??= LoadConfig();

        private static Config LoadConfig()
        {
            const string configPath = "config.json";
            if (!FileUtility.FileExists(FileUtility.FileType.StreamingAssets, configPath))
                throw new InvalidDataException("配置文件不存在：" + configPath);
            var jsonContent = FileUtility.ReadAllText(FileUtility.FileType.StreamingAssets, configPath);
            return JsonConvert.DeserializeObject<Config>(jsonContent);
        }

        public static string BuildOTAPostData(string macAddress, string boardName)
        {
            const string configPath = "ota.json";
            if (!FileUtility.FileExists(FileUtility.FileType.StreamingAssets, configPath))
                throw new InvalidDataException("配置文件不存在：" + configPath);
            var content = FileUtility.ReadAllText(FileUtility.FileType.StreamingAssets, configPath);
            content = content.Replace("{mac}", macAddress);
            content = content.Replace("{board_name}", boardName);
            return content;
        }

        private string _uuid;
        
        private string _macAddress;

        public string GetUUid()
        {
            _uuid ??= Guid.NewGuid().ToString("d");
            return _uuid;
        }
        
        public string GetMacAddress()
        {
            _macAddress ??= BuildMacAddress();
            return _macAddress;
        }

        private string BuildMacAddress()
        {
            if (!string.IsNullOrEmpty(Config.Instance.CustomMacAddress))
                return Config.Instance.CustomMacAddress;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver"))
            using (var settingsSecure = new AndroidJavaClass("android.provider.Settings$Secure"))
            {
                var androidId = settingsSecure.CallStatic<string>("getString", contentResolver, "android_id");
                var formattedId = string.Join(":", Enumerable.Range(2, 6).Select(i => androidId.Substring(i * 2, 2)));
                return formattedId;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            var vendorId = UnityEngine.iOS.Device.vendorIdentifier;
            if (!string.IsNullOrEmpty(vendorId))
            {
                vendorId = vendorId.Replace("-", "").Substring(vendorId.Length - 12, 12);
                return string.Join(":", Enumerable.Range(0, 6)
                    .Select(i => vendorId.Substring(i * 2, 2)));
            }
#else
            var adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            var adapter = adapters.OrderByDescending(i => i.Id).FirstOrDefault(i =>
                i.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                i.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                    or System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211);
            if (adapter != null)
            {
                var bytes = adapter.GetPhysicalAddress().GetAddressBytes();
                return string.Join(":", bytes.Select(b => b.ToString("x2")));
            }
#endif
            return string.Empty;
        }

        public string GetBoardName()
        {
            return Application.productName;
        }

        public string GetVersion()
        {
            return Application.version;
        }

        [JsonProperty("CUSTOM_MAC_ADDRESS")] public string CustomMacAddress { get; private set; }

        [JsonProperty("WEBSOCKET_URL")] public string WebSocketUrl { get; private set; }

        [JsonProperty("WEBSOCKET_ACCESS_TOKEN")]
        public string WebSocketAccessToken { get; private set; }

        [JsonProperty("OPUS_FRAME_DURATION_MS")]
        public int OpusFrameDurationMs { get; private set; }

        [JsonProperty("AUDIO_INPUT_SAMPLE_RATE")]
        public int AudioInputSampleRate { get; private set; }

        [JsonProperty("AUDIO_OUTPUT_SAMPLE_RATE")]
        public int AudioOutputSampleRate { get; private set; }
        
        [JsonProperty("SERVER_INPUT_SAMPLE_RATE")]
        public int ServerInputSampleRate { get; private set; }

        [JsonProperty("USE_WAKE_WORD_DETECT")] public bool UseWakeWordDetect { get; private set; }

        [JsonProperty("OTA_VERSION_URL")] public string OtaVersionUrl { get; private set; }

        [JsonProperty("KEYWORD_SPOTTER_MODEL_CONFIG_TRANSDUCER_ENCODER")]
        public string KeyWordSpotterModelConfigTransducerEncoder { get; private set; }

        [JsonProperty("KEYWORD_SPOTTER_MODEL_CONFIG_TRANSDUCER_DECODER")]
        public string KeyWordSpotterModelConfigTransducerDecoder { get; private set; }

        [JsonProperty("KEYWORD_SPOTTER_MODEL_CONFIG_TRANSDUCER_JOINER")]
        public string KeyWordSpotterModelConfigTransducerJoiner { get; private set; }

        [JsonProperty("KEYWORD_SPOTTER_MODEL_CONFIG_TOKEN")]
        public string KeyWordSpotterModelConfigToken { get; private set; }
        
        [JsonProperty("KEYWORD_SPOTTER_MODEL_CONFIG_NUM_THREADS")]
        public int KeyWordSpotterModelConfigNumThreads { get; private set; }

        [JsonProperty("KEYWORD_SPOTTER_KEYWORDS_FILE")]
        public string KeyWordSpotterKeyWordsFile { get; private set; }
        
        [JsonProperty("VAD_MODEL_CONFIG")]
        public string VadModelConfig { get; private set; }
    }
}