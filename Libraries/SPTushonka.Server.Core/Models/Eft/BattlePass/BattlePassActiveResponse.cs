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
    public List<BattlePass>? BattlePasses { get; set; }
}

public record BattlePass
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("exchangeRate")]
    public int? ExchangeRate { get; set; }

    [JsonPropertyName("itemExchangeSettings")]
    public BattlePassItemExchangeSettings? ItemExchangeSettings { get; set; }

    [JsonPropertyName("pages")]
    public List<BattlePassPage>? Pages { get; set; }

    [JsonPropertyName("documents")]
    public List<BattlePassDocument>? Documents { get; set; }

    [JsonPropertyName("documentLimits")]
    public BattlePassDocumentLimits? DocumentLimits { get; set; }
}

public record BattlePassItemExchangeSettings
{
    [JsonPropertyName("requiredDocuments")]
    public int? RequiredDocuments { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("itemId")]
    public MongoId ItemId { get; set; }
}

public record BattlePassPage
{
    [JsonPropertyName("prevPageItemsRequirement")]
    public int? PrevPageItemsRequirement { get; set; }

    [JsonPropertyName("rewards")]
    public List<BattlePassPageReward>? Rewards { get; set; }
}

public record BattlePassPageReward
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("location")]
    public RewardGridLocation? Location { get; set; }

    [JsonPropertyName("rewards")]
    public List<Reward>? Rewards { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("bigImageUrl")]
    public string? BigImageUrl { get; set; }

    [JsonPropertyName("cost")]
    public Dictionary<MongoId, int>? Cost { get; set; }
}

public record BattlePassDocument
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("unavailableImageUrl")]
    public string? UnavailableImageUrl { get; set; }

    [JsonPropertyName("itemId")]
    public MongoId ItemId { get; set; }
}

public record BattlePassDocumentLimits
{
    [JsonPropertyName("resetHours")]
    public int? ResetHours { get; set; }

    [JsonPropertyName("limitsByGameMode")]
    public List<BattlePassGameModeLimit>? LimitsByGameMode { get; set; }
}

public record BattlePassGameModeLimit
{
    [JsonPropertyName("gameMode")]
    public string? GameMode { get; set; }

    [JsonPropertyName("totalLimit")]
    public int? TotalLimit { get; set; }
}
