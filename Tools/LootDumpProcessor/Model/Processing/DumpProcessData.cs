using LootDumpProcessor.Storage;

namespace LootDumpProcessor.Model.Processing;

public class DumpProcessData
{
    public Dictionary<string, IKey> LooseLootCounts { get; set; } = new();
    public Dictionary<string, List<PreProcessedStaticLoot>> ContainerCounts { get; set; } = new();
    public Dictionary<string, int> MapCounts { get; set; } = new();
}
