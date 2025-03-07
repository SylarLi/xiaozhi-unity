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
            var configPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "config.json");
            if (!File.Exists(configPath)) throw new InvalidDataException("配置文件不存在：" + configPath);
            var jsonContent = File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<Config>(jsonContent);
        }

        public static string BuildOTAPostData(string macAddress, string boardName)
        {
            var configPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "ota.json");
            if (!File.Exists(configPath)) throw new InvalidDataException("配置文件不存在：" + configPath);
            var content = File.ReadAllText(configPath);
            content = content.Replace("{mac}", macAddress);
            content = content.Replace("{board_name}", boardName);
            return content;
        }

        [JsonProperty("WEBSOCKET_URL")] public string WebSocketUrl { get; set; }

        [JsonProperty("WEBSOCKET_ACCESS_TOKEN")]
        public string WebSocketAccessToken { get; set; }

        [JsonProperty("OPUS_FRAME_DURATION_MS")]
        public int OpusFrameDurationMs { get; set; }

        [JsonProperty("USE_AUDIO_PROCESSING")]
        public bool UseAudioProcessing { get; set; }

        [JsonProperty("AUDIO_INPUT_SAMPLE_RATE")]
        public int AudioInputSampleRate { get; set; }

        [JsonProperty("AUDIO_OUTPUT_SAMPLE_RATE")]
        public int AudioOutputSampleRate { get; set; }

        [JsonProperty("USE_WAKE_WORD_DETECT")]
        public bool UseWakeWordDetect { get; set; }
        
        [JsonProperty("AUDIO_INPUT_RESAMPLE_RATE")]
        public int AudioInputResampleRate { get; set; }
        
        [JsonProperty("OTA_VERSION_URL")]
        public string OtaVersionUrl { get; set; }
    }
}