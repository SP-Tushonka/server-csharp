using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Inventory;

public record SaveDialogueStateRequest : InventoryBaseActionRequestData
{
    [JsonPropertyName("nodePathTraveled")]
    public List<NodePathTraveled>? DialogueProgress { get; set; }
}

public class NodePathTraveled
{
    [JsonPropertyName("traderId")]
    public MongoId? TraderId { get; set; }

    [JsonPropertyName("dialogueId")]
    public MongoId? DialogueId { get; set; }

    [JsonPropertyName("nodeId")]
    public MongoId? NodeId { get; set; }
}
