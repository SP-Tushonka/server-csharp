using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Inventory;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record ReadQuestNoteRequest : InventoryBaseActionRequestData
{
    [JsonPropertyName("noteId")]
    public MongoId NoteId { get; set; }
}
