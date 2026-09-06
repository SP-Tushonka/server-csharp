using LootDumpProcessor.Serializers.Json.Converters;
using LootDumpProcessor.Storage;

namespace LootDumpProcessor.Model.Processing;

public class PreProcessedLooseLoot : IKeyable
{
    public Dictionary<string, int> Counts { get; set; }

    [System.Text.Json.Serialization.JsonConverter(typeof(NetJsonKeyConverter))]
    public IKey ItemProperties { get; set; }

    public int MapSpawnpointCount { get; set; }

    public IKey GetKey()
    {
        throw new NotImplementedException();
    }
}
