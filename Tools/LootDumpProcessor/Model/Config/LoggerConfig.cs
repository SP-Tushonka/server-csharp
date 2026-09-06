using System.Text.Json.Serialization;
using LootDumpProcessor.Logger;

namespace LootDumpProcessor.Model.Config;

public class LoggerConfig
{
    [JsonPropertyName("logLevel")]
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    [JsonPropertyName("queueLoggerPoolingTimeoutMs")]
    public int QueueLoggerPoolingTimeoutMs { get; set; } = 1000;
}
