using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Inventory;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record SetDisplayedEndingRequest : InventoryBaseActionRequestData
{
    [JsonPropertyName("endingId")]
    public string? EndingId { get; set; }
}
