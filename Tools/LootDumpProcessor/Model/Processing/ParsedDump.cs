using System.Text.RegularExpressions;
using LootDumpProcessor.Storage;

namespace LootDumpProcessor.Model.Processing;

public class ParsedDump : IKeyable
{
    private static readonly Regex _hashRegex = new("([^a-zA-Z0-9])");
    public BasicInfo BasicInfo { get; set; }
    public PreProcessedLooseLoot LooseLoot { get; set; }
    public List<PreProcessedStaticLoot> Containers { get; set; }

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

    public IKey GetKey()
    {
        var sanitizedHash = _hashRegex.Replace(BasicInfo.FileHash, "");
        return new SubdivisionedUniqueKey(
            new[] { "parsedDumps", BasicInfo.Map, $"{BasicInfo.FileName.Split("\\").Last().Replace(".", "")}-{sanitizedHash}" }
        );
    }
}
