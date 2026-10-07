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

    // Keyed by language, then by key such as "offer.<id>.name".
    [JsonPropertyName("locale")]
    public required Dictionary<string, Dictionary<string, string>> Locale { get; set; }
}

public sealed record ShopMenuItem
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    // Null for a tab that only groups its children.
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

    [JsonPropertyName("soundTheme")]
    public string? SoundTheme { get; set; }

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

// One tile on a page. Position and aspect ratio drive the grid the client lays out.
public sealed record ShopBlock
{
    [JsonPropertyName("offerId")]
    public MongoId OfferId { get; set; }

    [JsonPropertyName("nameKey")]
    public string? NameKey { get; set; }

    [JsonPropertyName("subtitleKey")]
    public string? SubtitleKey { get; set; }

    // A second line the shop renders in green under its own label.
    [JsonPropertyName("additionalSubtitleKey")]
    public string? AdditionalSubtitleKey { get; set; }

    [JsonPropertyName("additionalSubtitleLabelKey")]
    public string? AdditionalSubtitleLabelKey { get; set; }

    // Artwork for the purchase dialog, larger than the tile image.
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

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("widgetSettings")]
    public ShopWidgetSettings? WidgetSettings { get; set; }
}

public sealed record ShopWidgetSettings
{
    [JsonPropertyName("steam")]
    public ShopSteamSettings? Steam { get; set; }

    [JsonPropertyName("xsolla")]
    public ShopXsollaSettings? Xsolla { get; set; }
}

public sealed record ShopSteamSettings
{
    [JsonPropertyName("link")]
    public string? Link { get; set; }
}

public sealed record ShopXsollaSettings
{
    [JsonPropertyName("sku")]
    public string? Sku { get; set; }
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

    /// <summary>
    ///     What the item page's gallery shows
    /// </summary>
    [JsonPropertyName("additionalDemonstrationItems")]
    public List<ShopDemonstrationItem> DemonstrationItems { get; set; } = [];

    /// <summary>
    ///     What the buyer receives. A bundle's items are the offers it is made of
    /// </summary>
    [JsonPropertyName("items")]
    public List<ShopOfferItem> Items { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    ///     Bundles this offer is sold in. Buying the offer does not deliver them
    /// </summary>
    [JsonPropertyName("relatedOffers")]
    public List<ShopRelatedOffer> RelatedOffers { get; set; } = [];

    [JsonPropertyName("showBundleComposition")]
    public bool ShowBundleComposition { get; set; }

    [JsonPropertyName("showRelatedOffers")]
    public bool ShowRelatedOffers { get; set; }
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

public sealed record ShopDemonstrationItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = ShopDemonstrationItemType.Image;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    // A sound entry carries its picture here instead of in url.
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("thumbUrl")]
    public string? ThumbUrl { get; set; }

    /// <summary>
    ///     Voice line the game plays when a sound entry is clicked
    /// </summary>
    [JsonPropertyName("sound")]
    public string? Sound { get; set; }
}

public static class ShopDemonstrationItemType
{
    public const string Image = "Image";
    public const string Sound = "Sound";
}

public sealed record ShopOfferItem
{
    /// <summary>
    ///     Offer this entry stands for when it is part of a bundle
    /// </summary>
    [JsonPropertyName("id")]
    public MongoId? OfferId { get; set; }

    [JsonPropertyName("nameKey")]
    public string? NameKey { get; set; }

    [JsonPropertyName("descriptionKey")]
    public string? DescriptionKey { get; set; }

    [JsonPropertyName("images")]
    public List<ShopRelatedImage> Images { get; set; } = [];

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

    // What the entry hands over when it is neither an item nor a customisation.
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("rowsCount")]
    public int RowsCount { get; set; }

    [JsonPropertyName("edition")]
    public string? Edition { get; set; }

    [JsonPropertyName("persistAfterWipe")]
    public bool PersistAfterWipe { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("gameRewardConfig")]
    public ShopGameReward? GameReward { get; set; }
}

/// <summary>
///     A quest style reward a GAME_REWARD entry grants, such as an item stack sent by mail
/// </summary>
public sealed record ShopGameReward
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("target")]
    public MongoId? Target { get; set; }

    [JsonPropertyName("value")]
    public int Value { get; set; }

    /// <summary>
    ///     Get the item template this reward hands over, null when it is not an item
    /// </summary>
    public MongoId? ItemTemplate => Type == "Item" ? Target : null;
}

public enum ShopPurchaseOutcome
{
    Success,
    OfferNotFound,
    PurchaseLimitExceeded,
    NotDeliverable,
    InsufficientBalance,
}

/// <summary>
///     Result of a Shop purchase. The transaction is set on success and is what the game signs to complete it
/// </summary>
public sealed record ShopPurchaseReceipt(ShopPurchaseOutcome Outcome, string? TransactionId = null);

public static class ShopPurchaseOutcomeExtensions
{
    /// <summary>
    ///     Get the Shop locale key for a failed purchase
    /// </summary>
    /// <param name="result">Purchase outcome</param>
    /// <returns>The key between common.notification. and .summary or .detail</returns>
    public static string NotificationKey(this ShopPurchaseOutcome result)
    {
        return result switch
        {
            ShopPurchaseOutcome.OfferNotFound => "offerNotFoundError",
            ShopPurchaseOutcome.PurchaseLimitExceeded => "purchaseLimitExceededError",
            ShopPurchaseOutcome.InsufficientBalance => "insufficientBalanceError",
            _ => "error",
        };
    }
}

public static class ShopOfferItemType
{
    public const string Customization = "CUSTOMIZATION";
    public const string BattlePassUniversalDocument = "EFT_BATTLE_PASS_UNIVERSAL_DOCUMENT";
    public const string StashRows = "STASH_ROWS";
    public const string GameEdition = "GAME_EDITION";
    public const string PveMode = "PVE_MODE";
    public const string GameReward = "GAME_REWARD";
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
