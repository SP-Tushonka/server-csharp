using YamlDotNet.Serialization;

namespace LootDumpProcessor.Model.Config;

public class MapDirectoryMapping
{
    [YamlMember(Alias = "name")]
    public List<string> Name { get; set; }

    [YamlMember(Alias = "threshold_date")]
    public string ThresholdDate { get; set; }
}
