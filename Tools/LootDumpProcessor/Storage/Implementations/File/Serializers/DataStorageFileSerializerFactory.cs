namespace LootDumpProcessor.Storage.Implementations.File.Serializers;

public static class DataStorageFileSerializerFactory
{
    private static IDataStorageFileSerializer _dataStorageFileSerializer;

    public static IDataStorageFileSerializer GetInstance()
    {
        // TODO: implement real factory
        if (_dataStorageFileSerializer == null)
        {
            _dataStorageFileSerializer = new JsonDataStorageFileSerializer();
        }

        return _dataStorageFileSerializer;
    }
}
