using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Common;

public sealed class PointOfViewSoundVolume
{
    [JsonPropertyName("FpVolume")]
    public required float FpVolume { get; set; } = 0.5f;

    [JsonPropertyName("TpVolume")]
    public required float TpVolume { get; set; } = 1f;
}
