using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class Config
{
    private static Config _instance;
    public static Config Instance => _instance ??= LoadConfig();

    [JsonProperty("CONFIG_WEBSOCKET_URL")] public string WebSocketUrl { get; set; }

    [JsonProperty("CONFIG_WEBSOCKET_ACCESS_TOKEN")]
    public string WebSocketAccessToken { get; set; }

    private static Config LoadConfig()
    {
        var configPath = Path.Combine(Application.streamingAssetsPath, "config.json");
        if (!File.Exists(configPath)) throw new InvalidDataException("配置文件不存在：" + configPath);
        var jsonContent = File.ReadAllText(configPath);
        return JsonConvert.DeserializeObject<Config>(jsonContent);

    }
}