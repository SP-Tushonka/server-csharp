using System.Text.Json.Serialization;
using LootDumpProcessor.Serializers.Json.Converters;

namespace LootDumpProcessor.Model.Config;

public class DumpProcessorConfig
{
    [JsonPropertyName("spawnContainerChanceIncludeAfterDate")]
    [System.Text.Json.Serialization.JsonConverter(typeof(NetDateTimeConverter))]
    public DateTime SpawnContainerChanceIncludeAfterDate { get; set; }
}
