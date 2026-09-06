using LootDumpProcessor.Storage;

namespace LootDumpProcessor.Model.Processing;

public class PartialData
{
    public BasicInfo BasicInfo { get; set; }
    public IKey ParsedDumpKey { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is ParsedDump dump)
            return dump.BasicInfo.Equals(BasicInfo);
        return false;
    }

    public override int GetHashCode()
    {
        return BasicInfo.GetHashCode();
    }
}
