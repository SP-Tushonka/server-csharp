using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace SPTarkov.Server.Core.Utils.Json.Converters;

public class StringToSpectreColorConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32() switch
            {
                30 or 40 => Color.Black,
                31 or 41 => Color.Red,
                32 or 42 => Color.Green,
                33 or 43 => Color.Yellow,
                34 or 44 => Color.Blue,
                35 or 45 => Color.Magenta,
                36 or 46 => Color.Cyan,
                37 or 47 => Color.White,
                90 => Color.Gray,
                _ => Color.Default,
            };
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() switch
            {
                "Black" => Color.Black,
                "Red" => Color.Red,
                "Green" => Color.Green,
                "Yellow" => Color.Yellow,
                "Blue" => Color.Blue,
                "Magenta" => Color.Magenta,
                "Cyan" => Color.Cyan,
                "White" => Color.White,
                "Gray" => Color.Gray,
                _ => Color.Default,
            };
        }

        throw new JsonException($"The JsonTokenType was not of type string or number, it was: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.ToString(), options);
    }
}
