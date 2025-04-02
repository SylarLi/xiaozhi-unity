using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class Config
    {
        public static Config Instance { get; private set; }

        public static async UniTask LoadConfig()
        {
            const string configPath = "config.json";
            if (!FileUtility.FileExists(FileUtility.FileType.StreamingAssets, configPath))
                throw new InvalidDataException("配置文件不存在：" + configPath);
            var jsonContent = await FileUtility.ReadAllTextAsync(FileUtility.FileType.StreamingAssets, configPath);
            Instance = JsonConvert.DeserializeObject<Config>(jsonContent);
        }

        public static string BuildOtaPostData(string macAddress, string boardName)
        {
            const string configPath = "ota.json";
            if (!FileUtility.FileExists(FileUtility.FileType.StreamingAssets, configPath))
                throw new InvalidDataException("配置文件不存在：" + configPath);
            var content = FileUtility.ReadAllText(FileUtility.FileType.StreamingAssets, configPath);
            content = content.Replace("{mac}", macAddress);
            content = content.Replace("{board_name}", boardName);
            return content;
        }

        private static string _uuid;
        
        private static string _macAddress;

        public static string GetUUid()
        {
            _uuid ??= Guid.NewGuid().ToString("d");
            return _uuid;
        }
        
        public static string GetMacAddress()
        {
            _macAddress ??= BuildMacAddress();
            return _macAddress;
        }

        private static string BuildMacAddress()
        {
            if (!string.IsNullOrEmpty(Instance.CustomMacAddress))
                return Instance.CustomMacAddress;
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
            
            return string.Empty;
#endif
        }

        public static string GetBoardName()
        {
            return Application.productName;
        }

        public static string GetVersion()
        {
            return Application.version;
        }

        public static bool IsMobile()
        {
            return Application.isMobilePlatform;
        }
        
        [JsonProperty("LANG_CODE")]
        public string LangCode { get; private set; }

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

        [JsonProperty("ACTIVATION_URL")]
        public string ActivationURL { get; private set; }
    }
}