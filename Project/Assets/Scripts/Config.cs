using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using Newtonsoft.Json;
using UnityEngine;

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

    [JsonProperty("CONFIG_WEBSOCKET_URL")] public string WebSocketUrl { get; set; }

    [JsonProperty("CONFIG_WEBSOCKET_ACCESS_TOKEN")]
    public string WebSocketAccessToken { get; set; }

    [JsonProperty("OPUS_FRAME_DURATION_MS")]
    public int OpusFrameDurationMs { get; set; }

    [JsonProperty("CONFIG_USE_AUDIO_PROCESSING")]
    public bool UseAudioProcessing { get; set; }

    [JsonProperty("AUDIO_INPUT_SAMPLE_RATE")]
    public int AudioInputSampleRate { get; set; }

    [JsonProperty("AUDIO_OUTPUT_SAMPLE_RATE")]
    public int AudioOutputSampleRate { get; set; }

    // CONFIG_USE_WAKE_WORD_DETECT
    [JsonProperty("CONFIG_USE_WAKE_WORD_DETECT")]
    public bool UseWakeWordDetect { get; set; }
}