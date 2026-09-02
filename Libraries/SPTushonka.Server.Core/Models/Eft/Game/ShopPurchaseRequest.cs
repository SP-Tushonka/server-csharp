using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public sealed record ShopPurchaseRequest : IRequestData
{
    [JsonPropertyName("offerId")]
    public string? OfferId { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }
}

public sealed record ShopPurchaseResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
