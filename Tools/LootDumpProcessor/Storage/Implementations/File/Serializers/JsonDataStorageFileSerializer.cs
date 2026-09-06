using LootDumpProcessor.Serializers;
using LootDumpProcessor.Serializers.Json;

namespace LootDumpProcessor.Storage.Implementations.File.Serializers;

public class JsonDataStorageFileSerializer : IDataStorageFileSerializer
{
    public string GetExtension() => "json";

    public ISerializer GetSerializer()
    {
        return new NetJsonSerializer();
    }
}
