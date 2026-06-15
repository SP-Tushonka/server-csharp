using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Eft.Common;

[JsonConverter(typeof(PointOfViewSoundValueConverter))]
public sealed class PointOfViewSoundValue
{
    [JsonPropertyName("FpValue")]
    public required float FpValue { get; set; } = 0.5f;

    [JsonPropertyName("TpValue")]
    public required float TpValue { get; set; } = 1f;
}
