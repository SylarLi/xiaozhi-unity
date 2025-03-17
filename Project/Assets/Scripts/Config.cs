using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace XiaoZhi.Unity
{
    public class Config
    {
        private static Config _instance;
        public static Config Instance => _instance ??= LoadConfig();

        private static Config LoadConfig()
        {
            const string configPath = "config.json";
            if (!ResourceLoader.FileExists(ResourceLoader.ResourceType.StreamingAssets, configPath))
                throw new InvalidDataException("配置文件不存在：" + configPath);
            var jsonContent = ResourceLoader.ReadAllText(ResourceLoader.ResourceType.StreamingAssets, configPath);
            return JsonConvert.DeserializeObject<Config>(jsonContent);
        }

        public static string BuildOTAPostData(string macAddress, string boardName)
        {
            const string configPath = "ota.json";
            if (!ResourceLoader.FileExists(ResourceLoader.ResourceType.StreamingAssets, configPath))
                throw new InvalidDataException("配置文件不存在：" + configPath);
            var content = ResourceLoader.ReadAllText(ResourceLoader.ResourceType.StreamingAssets, configPath);
            content = content.Replace("{mac}", macAddress);
            content = content.Replace("{board_name}", boardName);
            return content;
        }

        [JsonProperty("CUSTOM_MAC_ADDRESS")]
        public string CustomMacAddress { get; set; }

        [JsonProperty("WEBSOCKET_URL")] public string WebSocketUrl { get; set; }

        [JsonProperty("WEBSOCKET_ACCESS_TOKEN")]
        public string WebSocketAccessToken { get; set; }

        [JsonProperty("OPUS_FRAME_DURATION_MS")]
        public int OpusFrameDurationMs { get; set; }

        [JsonProperty("USE_AUDIO_PROCESSING")] public bool UseAudioProcessing { get; set; }

        [JsonProperty("AUDIO_INPUT_SAMPLE_RATE")]
        public int AudioInputSampleRate { get; set; }

        [JsonProperty("AUDIO_OUTPUT_SAMPLE_RATE")]
        public int AudioOutputSampleRate { get; set; }

        [JsonProperty("USE_WAKE_WORD_DETECT")] public bool UseWakeWordDetect { get; set; }

        [JsonProperty("OTA_VERSION_URL")] public string OtaVersionUrl { get; set; }
    }
}