using LootDumpProcessor.Logger;
using LootDumpProcessor.Storage.Implementations.File.Serializers;

namespace LootDumpProcessor.Storage.Implementations.File.Handlers;

public abstract class AbstractStoreHandler : IStoreHandler
{
    protected readonly IDataStorageFileSerializer _serializer = DataStorageFileSerializerFactory.GetInstance();

    public void Store<T>(T obj, bool failIfDuplicate = true)
        where T : IKeyable
    {
        var locationWithFile = GetLocation(obj.GetKey());
        if (System.IO.File.Exists(locationWithFile) && failIfDuplicate)
        {
            //throw new Exception($"Attempted to save duplicated object into data storage: {locationWithFile}");
            LoggerFactory
                .GetInstance()
                .Log($"Attempted to save duplicated object into data storage: {locationWithFile}", LogLevel.Warning);
        }

        System.IO.File.WriteAllText(locationWithFile, _serializer.GetSerializer().Serialize(obj));
    }

    public T? Retrieve<T>(IKey obj)
        where T : IKeyable
    {
        var locationWithFile = GetLocation(obj);
        if (!System.IO.File.Exists(locationWithFile))
        {
            return default;
        }

        return _serializer.GetSerializer().Deserialize<T>(System.IO.File.ReadAllText(locationWithFile));
    }

    public bool Exists(IKey obj)
    {
        var locationWithFile = GetLocation(obj);
        return System.IO.File.Exists(locationWithFile);
    }

    public abstract List<T> RetrieveAll<T>()
        where T : IKeyable;

    protected abstract string GetLocation(IKey key);

    protected virtual string GetBaseLocation()
    {
        return Path.Combine(LootDumpProcessorContext.StorageCacheDirectory, "LootGen");
    }
}
