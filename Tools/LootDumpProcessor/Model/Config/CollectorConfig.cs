using System.Text.Json.Serialization;
using LootDumpProcessor.Process.Collector;

namespace LootDumpProcessor.Model.Config;

public class CollectorConfig
{
    [JsonPropertyName("collectorType")]
    public CollectorType CollectorType { get; set; }

    [JsonPropertyName("maxEntitiesBeforeDumping")]
    public int MaxEntitiesBeforeDumping { get; set; }
}
