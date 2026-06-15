using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTarkov.Server.Core.Models.Eft.Profile;

public sealed record TutorGameCheckRequest : IRequestData
{
    [JsonPropertyName("skipTutorial")]
    public bool SkipTutorial { get; set; }
}
