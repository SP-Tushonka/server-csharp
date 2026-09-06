using LootDumpProcessor.Storage.Implementations.File;
using LootDumpProcessor.Storage.Implementations.Memory;

namespace LootDumpProcessor.Storage;

public static class DataStorageFactory
{
    private static readonly Dictionary<DataStorageTypes, IDataStorage> _dataStorage = new();

    private static object lockObject = new();

    /**
     * Requires LootDumpProcessorContext to be initialized before using
     */
    public static IDataStorage GetInstance()
    {
        return GetInstance(DataStorageTypes.Memory);
    }

    public static IDataStorage GetInstance(DataStorageTypes type)
    {
        IDataStorage dataStorage;
        lock (lockObject)
        {
            if (!_dataStorage.TryGetValue(type, out dataStorage))
            {
                dataStorage = type switch
                {
                    DataStorageTypes.File => new FileDataStorage(),
                    DataStorageTypes.Memory => new MemoryDataStorage(),
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                };

                _dataStorage.Add(type, dataStorage);
            }
        }

        return dataStorage;
    }
}
