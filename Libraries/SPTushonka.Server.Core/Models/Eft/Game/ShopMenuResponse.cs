using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public sealed record ShopMenuResponse
{
    [JsonPropertyName("data")]
    public ShopMenuData? Data { get; set; }
}

public sealed record ShopMenuData
{
    [JsonPropertyName("items")]
    public List<ShopMenuEntry>? Items { get; set; }
}

public sealed record ShopMenuEntry
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("price")]
    public int Price { get; set; }
}

public sealed record ShopCatalogItemResponse
{
    [JsonPropertyName("data")]
    public ShopCatalogItemData? Data { get; set; }
}

public sealed record ShopCatalogItemData
{
    [JsonPropertyName("item")]
    public ShopCatalogItemBody? Item { get; set; }
}

public sealed record ShopCatalogItemBody
{
    [JsonPropertyName("items")]
    public List<ShopOfferItem>? Items { get; set; }
}
