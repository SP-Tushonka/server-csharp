using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

/// <summary>
///     Response of /client/ending/list.
/// </summary>
public record EndingsResponse
{
    [JsonPropertyName("elements")]
    public required List<EndingElement> Elements { get; set; }
}

public record EndingElement
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("systemName")]
    public string? SystemName { get; set; }

    [JsonPropertyName("conditions")]
    public List<QuestCondition>? Conditions { get; set; }

    [JsonPropertyName("rewards")]
    public List<Reward>? Rewards { get; set; }

    [JsonPropertyName("consequences")]
    public List<EndingConsequence>? Consequences { get; set; }
}

public record EndingConsequence
{
    [JsonPropertyName("viewId")]
    public string? ViewId { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("localizationKeyCaptionPVP")]
    public string? LocalizationKeyCaptionPvp { get; set; }

    [JsonPropertyName("localizationKeyCaptionPVE")]
    public string? LocalizationKeyCaptionPve { get; set; }
}
