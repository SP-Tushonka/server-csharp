using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;

namespace SPTarkov.Server.Core.Models.Eft.BattlePass;

/// <summary>
///     Sent when season documents are traded in, for another kind of document or for the pass's container.
/// </summary>
public record BattlePassExchangeDocumentsRequest : BaseInteractionRequestData
{
    [JsonPropertyName("battlePassId")]
    public MongoId BattlePassId { get; set; }

    /// <summary>The document scheme to receive, absent when trading for the container</summary>
    [JsonPropertyName("receiveDocumentId")]
    public MongoId? ReceiveDocumentId { get; set; }

    [JsonPropertyName("items")]
    public List<BattlePassHandIn>? Items { get; set; }
}
