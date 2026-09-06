using LootDumpProcessor.Model.Input;

namespace LootDumpProcessor.Model.Processing;

public class BasicInfo
{
    public required string Map { get; set; }
    public required string FileHash { get; set; }
    public Data? Data { get; set; }
    public DateTime Date { get; set; }
    public required string FileName { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is BasicInfo info)
            return FileHash == info.FileHash;
        return false;
    }

    public override int GetHashCode()
    {
        return FileHash.GetHashCode();
    }
}
