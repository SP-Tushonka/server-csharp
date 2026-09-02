using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Inventory;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record AddQuestNoteRequest : InventoryBaseActionRequestData
{
    [JsonPropertyName("questId")]
    public MongoId QuestId { get; set; }

    [JsonPropertyName("conditionId")]
    public MongoId ConditionId { get; set; }

    [JsonPropertyName("noteId")]
    public MongoId NoteId { get; set; }

    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }
}
