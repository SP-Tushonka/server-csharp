using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Seasons;

namespace SPTarkov.Server.Core.Models.Eft.BattlePass;

/// <summary>
///     Response of /client/battle-pass/active.
/// </summary>
public record BattlePassActiveResponse
{
    [JsonPropertyName("battlePasses")]
    public required List<BattlePass> BattlePasses { get; set; }
}

public record BattlePass
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("exchangeRate")]
    public required int ExchangeRate { get; set; }

    [JsonPropertyName("itemExchangeSettings")]
    public required BattlePassItemExchangeSettings ItemExchangeSettings { get; set; }

    [JsonPropertyName("pages")]
    public required List<BattlePassPage> Pages { get; set; }

    [JsonPropertyName("documents")]
    public required List<BattlePassDocument> Documents { get; set; }

    [JsonPropertyName("documentLimits")]
    public required BattlePassDocumentLimits DocumentLimits { get; set; }
}

public record BattlePassItemExchangeSettings
{
    [JsonPropertyName("requiredDocuments")]
    public required int RequiredDocuments { get; set; }

    [JsonPropertyName("image")]
    public required string Image { get; set; }

    [JsonPropertyName("itemId")]
    public MongoId ItemId { get; set; }
}

public record BattlePassPage
{
    [JsonPropertyName("prevPageItemsRequirement")]
    public required int PrevPageItemsRequirement { get; set; }

    [JsonPropertyName("rewards")]
    public required List<BattlePassPageReward> Rewards { get; set; }
}

public record BattlePassPageReward
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("location")]
    public required RewardGridLocation Location { get; set; }

    [JsonPropertyName("rewards")]
    public required List<Reward> Rewards { get; set; }

    [JsonPropertyName("imageUrl")]
    public required string ImageUrl { get; set; }

    [JsonPropertyName("bigImageUrl")]
    public required string BigImageUrl { get; set; }

    [JsonPropertyName("cost")]
    public Dictionary<MongoId, int>? Cost { get; set; }
}

public record BattlePassDocument
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("imageUrl")]
    public required string ImageUrl { get; set; }

    [JsonPropertyName("unavailableImageUrl")]
    public required string UnavailableImageUrl { get; set; }

    [JsonPropertyName("itemId")]
    public MongoId ItemId { get; set; }
}

public record BattlePassDocumentLimits
{
    [JsonPropertyName("resetHours")]
    public required int ResetHours { get; set; }

    [JsonPropertyName("limitsByGameMode")]
    public required List<BattlePassGameModeLimit> LimitsByGameMode { get; set; }
}

public record BattlePassGameModeLimit
{
    [JsonPropertyName("gameMode")]
    public required string GameMode { get; set; }

    [JsonPropertyName("totalLimit")]
    public required int TotalLimit { get; set; }
}
