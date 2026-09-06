using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LootDumpProcessor.Model.Input;

// Only the fields the processor reads from a raid dump. The rest of the response is ignored.
public class Data
{
    [JsonPropertyName("locationLoot")]
    public required LocationLoot LocationLoot { get; set; }
}

public class LocationLoot
{
    [JsonPropertyName("Id")]
    public required string Id { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Loot")]
    public required List<SpawnpointTemplate> Loot { get; set; }
}
