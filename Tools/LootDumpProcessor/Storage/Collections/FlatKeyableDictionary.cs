using System.Text.Json.Serialization;
using LootDumpProcessor.Utils;

namespace LootDumpProcessor.Storage.Collections;

public class FlatKeyableDictionary<K, V> : Dictionary<K, V>, IKeyable
{
    [JsonPropertyName("__id__")]
    public string __ID { get; set; } = KeyGenerator.GetNextKey();

    public IKey GetKey()
    {
        return new FlatUniqueKey([__ID]);
    }
}
