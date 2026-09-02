using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace SPTarkov.Server.Core.Models.Eft.Profile;

public sealed record TutorGameProfileResponse
{
    [JsonPropertyName("profile")]
    public PmcData? Profile { get; set; }
}
