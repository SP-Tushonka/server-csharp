using System.Text.Json.Serialization;

namespace LootDumpProcessor.Model.Config;

public class ProcessorConfig
{
    [JsonPropertyName("spawnPointToleranceForForced")]
    public double SpawnPointToleranceForForced { get; set; } = 99D;

    [JsonPropertyName("looseLootCountTolerancePercentage")]
    public double LooseLootCountTolerancePercentage { get; set; } = 75D;
}
