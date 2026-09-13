using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Models.Eft.Hideout;

public record HideoutCustomisation
{
    [JsonPropertyName("globals")]
    public required List<HideoutCustomisationGlobal> Globals { get; set; }

    [JsonPropertyName("slots")]
    public required List<HideoutCustomisationSlot> Slots { get; set; }
}

public record HideoutCustomisationGlobal
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("conditions")]
    public required List<QuestCondition> Conditions { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("index")]
    public required int Index { get; set; }

    [JsonPropertyName("systemName")]
    public required string SystemName { get; set; }

    [JsonPropertyName("isEnabled")]
    public required bool IsEnabled { get; set; }

    [JsonPropertyName("itemId")]
    public required MongoId ItemId { get; set; }
}

public record HideoutCustomisationSlot
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("conditions")]
    public required List<QuestCondition> Conditions { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("index")]
    public required int Index { get; set; }

    [JsonPropertyName("systemName")]
    public required string SystemName { get; set; }

    [JsonPropertyName("isEnabled")]
    public required bool IsEnabled { get; set; }

    [JsonPropertyName("slotId")]
    public required string SlotId { get; set; }

    [JsonPropertyName("areaTypeId")]
    public required int AreaTypeId { get; set; }
}
