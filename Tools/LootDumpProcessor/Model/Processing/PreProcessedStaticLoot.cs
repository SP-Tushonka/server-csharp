using SPTarkov.Server.Core.Models.Eft.Common;

namespace LootDumpProcessor.Model.Processing;

public class PreProcessedStaticLoot
{
    public string Type { get; set; }
    public string ContainerId { get; set; }
    public List<SptLootItem> Items { get; set; }
}
