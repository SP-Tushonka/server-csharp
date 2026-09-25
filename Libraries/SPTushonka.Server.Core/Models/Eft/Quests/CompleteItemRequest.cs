using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Inventory;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record CompleteItemRequest : InventoryBaseActionRequestData
{
    /// <summary>
    ///     Template of the note or tape the player read
    /// </summary>
    [JsonPropertyName("completableItemId")]
    public MongoId CompletableItemId { get; set; }
}
