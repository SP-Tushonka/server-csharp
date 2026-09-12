using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace QuestVariableGenerator.Models;

/// <summary>
///     A number of flags we know are set by some of these quests, without knowing which quest sets which flag.
///     Completing any of the quests sets one flag, up to the count.
/// </summary>
public record FillerClass
{
    [JsonPropertyName("members")]
    public required int Members { get; init; }

    [JsonPropertyName("quests")]
    public required List<MongoId> Quests { get; init; }
}
