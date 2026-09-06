namespace LootDumpProcessor.Serializers;

public interface ISerializer
{
    string Serialize<T>(T obj);
    T? Deserialize<T>(string obj);
}
