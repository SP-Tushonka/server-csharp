using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace SPTarkov.Server.Core.Utils.Json.Converters;

public sealed class PointOfViewSoundValueConverter : JsonConverter<PointOfViewSoundValue>
{
    public override PointOfViewSoundValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected object.");
        }

        var fpValue = 0.5f;
        var tpValue = 1f;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new PointOfViewSoundValue { FpValue = fpValue, TpValue = tpValue };
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            var propertyName = reader.GetString();

            reader.Read();

            switch (propertyName)
            {
                case "FpValue":
                    fpValue = reader.GetSingle();
                    break;

                case "TpValue":
                    tpValue = reader.GetSingle();
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unexpected end of JSON.");
    }

    public override void Write(Utf8JsonWriter writer, PointOfViewSoundValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("FpValue", value.FpValue);
        writer.WriteNumber("TpValue", value.TpValue);

        writer.WriteEndObject();
    }
}
