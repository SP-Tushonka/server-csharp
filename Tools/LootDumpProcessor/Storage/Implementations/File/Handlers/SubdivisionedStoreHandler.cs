namespace LootDumpProcessor.Storage.Implementations.File.Handlers;

public class SubdivisionedStoreHandler : AbstractStoreHandler
{
    public override List<T> RetrieveAll<T>()
    {
        throw new NotImplementedException();
    }

    protected override string GetLocation(IKey key)
    {
        var location = $"{GetBaseLocation()}/{string.Join("/", key.GetLookupIndex().SkipLast(1))}";

        if (!Directory.Exists(location))
        {
            Directory.CreateDirectory(location);
        }

        return $"{location}/{key.GetLookupIndex().Last()}.{_serializer.GetExtension()}";
    }

    protected override string GetBaseLocation()
    {
        return $"{base.GetBaseLocation()}/subdivisioned";
    }
}
