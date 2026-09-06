namespace LootDumpProcessor.Storage.Implementations.File.Handlers;

public class FlatStoreHandler : AbstractStoreHandler
{
    public override List<T> RetrieveAll<T>()
    {
        throw new NotImplementedException();
    }

    protected override string GetLocation(IKey key)
    {
        var baseLocation = GetBaseLocation();
        if (!Directory.Exists(baseLocation))
        {
            Directory.CreateDirectory(baseLocation);
        }

        return $"{baseLocation}/{string.Join("-", key.GetLookupIndex())}.{_serializer.GetExtension()}";
    }

    protected override string GetBaseLocation()
    {
        return $"{base.GetBaseLocation()}/flat";
    }
}
