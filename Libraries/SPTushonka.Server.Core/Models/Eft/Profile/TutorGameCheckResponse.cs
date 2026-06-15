using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Profile;

public sealed record TutorGameCheckResponse
{
    /// <summary>
    ///     Launches the tutorial mission if true
    /// </summary>
    [JsonPropertyName("launchTutorGame")]
    public bool LaunchTutorGame { get; set; }
}
