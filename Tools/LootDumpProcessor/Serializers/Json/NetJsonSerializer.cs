using System.Text.Json;
using System.Text.Json.Serialization;
using LootDumpProcessor.Serializers.Json.Converters;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace LootDumpProcessor.Serializers.Json;

public class NetJsonSerializer : IJsonSerializer
{
    private static JsonSerializerOptions _serializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters =
        {
            new NetJsonKeyConverter(),
            new JsonStringEnumConverter(),
            new NetDateTimeConverter(),
            new StringToMongoIdConverter(),
        },
    };

    public string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, _serializeOptions);
    }

    public T? Deserialize<T>(string obj)
    {
        return JsonSerializer.Deserialize<T>(obj, _serializeOptions);
    }
}
