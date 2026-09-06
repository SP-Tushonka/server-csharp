using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace Common.Json
{
    public static class ToolJson
    {
        // JsonUtil keeps its options in static fields, so the tool shares one instance
        public static readonly JsonUtil Util = new([new ToolJsonConverterRegistrator(), new SptJsonConverterRegistrator()]);
    }

    // Registered ahead of the server converters so bot files keep slot names where the server would write enum ints
    public class ToolJsonConverterRegistrator : IJsonConverterRegistrator
    {
        public IEnumerable<JsonConverter> GetJsonConverters()
        {
            return [new EquipmentSlotsNameConverter()];
        }
    }

    public class EquipmentSlotsNameConverter : JsonConverter<EquipmentSlots>
    {
        public override EquipmentSlots Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return Enum.Parse<EquipmentSlots>(reader.GetString(), true);
        }

        public override void Write(Utf8JsonWriter writer, EquipmentSlots value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }

        public override EquipmentSlots ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return Enum.Parse<EquipmentSlots>(reader.GetString(), true);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, EquipmentSlots value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(value.ToString());
        }
    }
}
