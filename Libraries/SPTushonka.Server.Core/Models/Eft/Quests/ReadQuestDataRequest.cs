using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Inventory;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record ReadQuestDataRequest : InventoryBaseActionRequestData
{
    [JsonPropertyName("id")]
    public MongoId QuestId { get; set; }
}
