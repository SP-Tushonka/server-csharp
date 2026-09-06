namespace LootDumpProcessor.Storage;

public class SubdivisionedUniqueKey(string[] indexes) : AbstractKey(indexes)
{
    public override KeyType GetKeyType() => KeyType.Subdivisioned;
}
