using System.Text.Json.Serialization;
using LootDumpProcessor.Serializers.Json;

namespace LootDumpProcessor.Model.Config;

public class Config
{
    [JsonPropertyName("serverItemsJsonLocation")]
    public string? ServerItemsJsonLocation { get; set; }

    [JsonPropertyName("threads")]
    public int Threads { get; set; } = 6;

    [JsonPropertyName("threadPoolingTimeoutMs")]
    public int ThreadPoolingTimeoutMs { get; set; } = 1000;

    [JsonPropertyName("manualGarbageCollectionCalls")]
    public bool ManualGarbageCollectionCalls { get; set; }

    [JsonPropertyName("loggerConfig")]
    public LoggerConfig LoggerConfig { get; set; }

    [JsonPropertyName("readerConfig")]
    public ReaderConfig ReaderConfig { get; set; }

    [JsonPropertyName("processorConfig")]
    public ProcessorConfig ProcessorConfig { get; set; }

    [JsonPropertyName("dumpProcessorConfig")]
    public DumpProcessorConfig DumpProcessorConfig { get; set; }

    [JsonPropertyName("collectorConfig")]
    public CollectorConfig CollectorConfig { get; set; }

    [JsonPropertyName("containerIgnoreList")]
    public Dictionary<string, string[]> ContainerIgnoreList { get; set; }

    [JsonPropertyName("mapsToProcess")]
    public List<string> MapsToProcess { get; set; }
}
