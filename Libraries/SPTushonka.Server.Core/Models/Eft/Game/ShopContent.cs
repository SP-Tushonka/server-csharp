using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public sealed record ShopContent
{
    [JsonPropertyName("menu")]
    public required List<ShopMenuItem> Menu { get; set; }

    [JsonPropertyName("pages")]
    public required List<ShopPage> Pages { get; set; }

    [JsonPropertyName("offers")]
    public required List<ShopOffer> Offers { get; set; }

    [JsonPropertyName("prices")]
    public required List<ShopPrice> Prices { get; set; }

    /// <summary>Localisation keyed by language, then by key - "offer.&lt;id&gt;.name"</summary>
    [JsonPropertyName("locale")]
    public required Dictionary<string, Dictionary<string, string>> Locale { get; set; }
}

public sealed record ShopMenuItem
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    /// <summary>
    ///     Null for a tab that only groups its children rather than opening a page of its own.
    /// </summary>
    [JsonPropertyName("pageId")]
    public MongoId? PageId { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("nameKey")]
    public string? NameKey { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("selectedIconUrl")]
    public string? SelectedIconUrl { get; set; }

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = [];
}

public sealed record ShopPage
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("blocks")]
    public List<ShopBlock> Blocks { get; set; } = [];
}

/// <summary>One tile on a page. Position and aspect ratio drive the grid the client lays out.</summary>
public sealed record ShopBlock
{
    [JsonPropertyName("offerId")]
    public MongoId OfferId { get; set; }

    [JsonPropertyName("nameKey")]
    public string? NameKey { get; set; }

    [JsonPropertyName("subtitleKey")]
    public string? SubtitleKey { get; set; }

    /// <summary>A second line the shop renders in green, with its own label - "Includes", say.</summary>
    [JsonPropertyName("additionalSubtitleKey")]
    public string? AdditionalSubtitleKey { get; set; }

    [JsonPropertyName("additionalSubtitleLabelKey")]
    public string? AdditionalSubtitleLabelKey { get; set; }

    /// <summary>Artwork for the purchase dialog, which is usually larger than the tile image.</summary>
    [JsonPropertyName("purchasePopupImage")]
    public string? PurchasePopupImage { get; set; }

    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = [];

    [JsonPropertyName("position")]
    public ShopGridPosition Position { get; set; } = new();

    [JsonPropertyName("aspectRatio")]
    public string AspectRatio { get; set; } = "1:1";

    [JsonPropertyName("purchaseMethod")]
    public string PurchaseMethod { get; set; } = "INTERNAL_CURRENCY";

    [JsonPropertyName("itemsCount")]
    public int ItemsCount { get; set; } = 1;

    [JsonPropertyName("countable")]
    public bool Countable { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("scope")]
    public List<string> Scope { get; set; } = ["EFT"];
}

public sealed record ShopGridPosition
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

public sealed record ShopOffer
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("nameKey")]
    public string? NameKey { get; set; }

    [JsonPropertyName("descriptionKey")]
    public string? DescriptionKey { get; set; }

    [JsonPropertyName("subtitleKey")]
    public string? SubtitleKey { get; set; }

    [JsonPropertyName("purchaseMethod")]
    public string PurchaseMethod { get; set; } = "INTERNAL_CURRENCY";

    [JsonPropertyName("countable")]
    public bool Countable { get; set; }

    [JsonPropertyName("detailImages")]
    public List<ShopDetailImage> DetailImages { get; set; } = [];

    /// <summary>What the buyer receives. Items reference a template, customisations a suite.</summary>
    [JsonPropertyName("items")]
    public List<ShopOfferItem> Items { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    ///     What a bundle is made of. The item view lists these as its contents, each with its own
    ///     name, description and artwork.
    /// </summary>
    [JsonPropertyName("relatedOffers")]
    public List<ShopRelatedOffer> RelatedOffers { get; set; } = [];

    [JsonPropertyName("showBundleComposition")]
    public bool ShowBundleComposition { get; set; }
}

public sealed record ShopRelatedOffer
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("nameKey")]
    public string? NameKey { get; set; }

    [JsonPropertyName("descriptionKey")]
    public string? DescriptionKey { get; set; }

    [JsonPropertyName("images")]
    public List<ShopRelatedImage> Images { get; set; } = [];
}

public sealed record ShopRelatedImage
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("aspectRatio")]
    public string? AspectRatio { get; set; }
}

public sealed record ShopDetailImage
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("thumbUrl")]
    public string? ThumbUrl { get; set; }
}

public sealed record ShopOfferItem
{
    [JsonPropertyName("_tpl")]
    public MongoId? Template { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    [JsonPropertyName("customizationId")]
    public MongoId? CustomizationId { get; set; }

    [JsonPropertyName("customizationType")]
    public string? CustomizationType { get; set; }

    [JsonPropertyName("isApplyOnce")]
    public bool IsApplyOnce { get; set; }

    /// <summary>
    ///     What the entry hands over when it is neither an item nor a customisation
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public static class ShopOfferItemType
{
    public const string Customization = "CUSTOMIZATION";
    public const string BattlePassUniversalDocument = "EFT_BATTLE_PASS_UNIVERSAL_DOCUMENT";
}

public sealed record ShopPrice
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("amountWithDiscount")]
    public int AmountWithDiscount { get; set; }

    [JsonPropertyName("multiplier")]
    public int Multiplier { get; set; } = 1;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "AVAILABLE";
}
