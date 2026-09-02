using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

/// <summary>
///     Response of /client/quest/chains: the first quest of each chain mapped to the quests that follow it.
/// </summary>
public record QuestChainsResponse
{
    [JsonPropertyName("elements")]
    public Dictionary<MongoId, List<MongoId>>? Elements { get; set; }
}
