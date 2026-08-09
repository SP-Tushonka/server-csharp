using System.Text.Json;
using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Utils.Json.Converters;

internal class ToStringJsonConverter<T> : JsonConverter<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            try
            {
                return (T?)Activator.CreateInstance(typeToConvert, value);
            }
            catch (Exception ex)
            {
                throw new JsonException($"Unable to convert \"{value}\" to {typeof(T).Name}.", ex);
            }
        }

        throw new JsonException($"Expected string to deserialize {typeof(T).Name} but got {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
