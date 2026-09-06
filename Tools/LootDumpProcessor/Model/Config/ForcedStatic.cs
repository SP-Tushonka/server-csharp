using YamlDotNet.Serialization;

namespace LootDumpProcessor.Model.Config;

public class ForcedStatic
{
    [YamlMember(Alias = "static_weapon_ids")]
    public List<string> StaticWeaponIds { get; set; }

    [YamlMember(Alias = "forced_items")]
    public Dictionary<string, List<ForcedStaticEntry>> ForcedItems { get; set; }
}

public class ForcedStaticEntry
{
    [YamlMember(Alias = "containerId")]
    public string ContainerId { get; set; }

    [YamlMember(Alias = "itemTpl")]
    public string ItemTpl { get; set; }
}
