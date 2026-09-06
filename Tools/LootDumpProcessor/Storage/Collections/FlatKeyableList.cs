using LootDumpProcessor.Utils;

namespace LootDumpProcessor.Storage.Collections;

public class FlatKeyableList<T> : List<T>, IKeyable
{
    public string __ID { get; } = KeyGenerator.GetNextKey();

    public IKey GetKey()
    {
        return new FlatUniqueKey([__ID]);
    }
}
