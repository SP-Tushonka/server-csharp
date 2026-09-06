namespace LootDumpProcessor.Storage;

public class FlatUniqueKey(string[] indexes) : AbstractKey(indexes)
{
    public override KeyType GetKeyType() => KeyType.Unique;
}
