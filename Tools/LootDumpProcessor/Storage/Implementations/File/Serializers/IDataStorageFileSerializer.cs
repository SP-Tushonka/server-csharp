using LootDumpProcessor.Serializers;

namespace LootDumpProcessor.Storage.Implementations.File.Serializers;

public interface IDataStorageFileSerializer
{
    string GetExtension();
    ISerializer GetSerializer();
}
