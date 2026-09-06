using System.Text.Json;
using System.Text.Json.Serialization;
using LootDumpProcessor.Storage;

namespace LootDumpProcessor.Serializers.Json.Converters;

public class NetJsonKeyConverter : JsonConverter<IKey?>
{
    public override IKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Dictionary<string, string> values = new Dictionary<string, string>();
        while (reader.Read())
        {
            var property = reader.GetString() ?? "";
            reader.Read();
            var value = reader.GetString() ?? "";
            values.Add(property, value);
            if (values.Count == 2)
                break;
        }

        reader.Read();

        if (!values.TryGetValue("type", out var type))
        {
            throw new Exception("Key type was missing from json definition");
        }

        if (!values.TryGetValue("serializedKey", out var serializedKey))
        {
            throw new Exception("Key serializedKey was missing from json definition");
        }

        AbstractKey key = Enum.Parse<KeyType>(type) switch
        {
            KeyType.Subdivisioned => new SubdivisionedUniqueKey(serializedKey.Split("|")),
            KeyType.Unique => new FlatUniqueKey(serializedKey.Split("|")),
            _ => throw new Exception("Unknown key type used!"),
        };

        return key;
    }

    public override void Write(Utf8JsonWriter writer, IKey? value, JsonSerializerOptions options)
    {
        if (value != null)
        {
            var actualValue = (AbstractKey)value;
            writer.WriteStartObject();
            // writer.WritePropertyName("type");
            writer.WriteString("type", actualValue.GetKeyType().ToString());
            // writer.WritePropertyName("serializedKey");
            writer.WriteString("serializedKey", actualValue.SerializedKey);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
    }
}
