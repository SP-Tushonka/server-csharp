using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Spt.Tables.Globals;

public record RagfairGlobals
{
    public required double ChangePriceCoef { get; set; }

    public required List<RagFairItemRestriction> ItemRestrictions { get; set; }

    public required bool RagfairMinUserLevelByCategory { get; set; }

    public required double RagfairTurnOnTimestamp { get; set; }

    [JsonPropertyName("balancerAveragePriceCoefficient")]
    public required double BalancerAveragePriceCoefficient { get; set; }

    [JsonPropertyName("balancerMinPriceCount")]
    public required double BalancerMinPriceCount { get; set; }

    [JsonPropertyName("balancerRemovePriceCoefficient")]
    public required double BalancerRemovePriceCoefficient { get; set; }

    [JsonPropertyName("balancerUserItemSaleCooldown")]
    public required double BalancerUserItemSaleCooldown { get; set; }

    [JsonPropertyName("balancerUserItemSaleCooldownEnabled")]
    public required bool BalancerUserItemSaleCooldownEnabled { get; set; }

    [JsonPropertyName("communityItemTax")]
    public required double CommunityItemTax { get; set; }

    [JsonPropertyName("communityRequirementTax")]
    public required double CommunityRequirementTax { get; set; }

    [JsonPropertyName("communityTax")]
    public required double CommunityTax { get; set; }

    [JsonPropertyName("delaySinceOfferAdd")]
    public required double DelaySinceOfferAdd { get; set; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; set; }

    [JsonPropertyName("includePveTraderSales")]
    public required bool IncludePveTraderSales { get; set; }

    [JsonPropertyName("isOnlyFoundInRaidAllowed")]
    public required bool IsOnlyFoundInRaidAllowed { get; set; }

    [JsonPropertyName("maxActiveOfferCount")]
    public required List<RagFairActiveOfferCount> MaxActiveOfferCount { get; set; }

    [JsonPropertyName("maxRenewOfferTimeInHour")]
    public required double MaxRenewOfferTimeInHour { get; set; }

    [JsonPropertyName("maxSumForDecreaseRatingPerOneSale")]
    public required double MaxSumForDecreaseRatingPerOneSale { get; set; }

    [JsonPropertyName("maxSumForIncreaseRatingPerOneSale")]
    public required double MaxSumForIncreaseRatingPerOneSale { get; set; }

    [JsonPropertyName("maxSumForRarity")]
    public required Dictionary<string, RagFairRarityValue> MaxSumForRarity { get; set; }

    [JsonPropertyName("minUserLevel")]
    public required double MinUserLevel { get; set; }

    [JsonPropertyName("offerDurationTimeInHour")]
    public required double OfferDurationTimeInHour { get; set; }

    [JsonPropertyName("offerDurationTimeInHourAfterRemove")]
    public required double OfferDurationTimeInHourAfterRemove { get; set; }

    [JsonPropertyName("offerPriorityCost")]
    public required double OfferPriorityCost { get; set; }

    [JsonPropertyName("priceStabilizerEnabled")]
    public required bool PriceStabilizerEnabled { get; set; }

    [JsonPropertyName("priceStabilizerStartIntervalInHours")]
    public required double PriceStabilizerStartIntervalInHours { get; set; }

    [JsonPropertyName("priorityTimeModifier")]
    public required double PriorityTimeModifier { get; set; }

    [JsonPropertyName("ratingDecreaseCount")]
    public required double RatingDecreaseCount { get; set; }

    [JsonPropertyName("ratingIncreaseCount")]
    public required double RatingIncreaseCount { get; set; }

    [JsonPropertyName("ratingSumForDecrease")]
    public required double RatingSumForDecrease { get; set; }

    [JsonPropertyName("ratingSumForIncrease")]
    public required double RatingSumForIncrease { get; set; }

    [JsonPropertyName("renewPricePerHour")]
    public required double RenewPricePerHour { get; set; }

    [JsonPropertyName("sellInOnePiece")]
    public required double SellInOnePiece { get; set; }

    [JsonPropertyName("uniqueBuyerTimeoutInDays")]
    public required double UniqueBuyerTimeoutInDays { get; set; }

    [JsonPropertyName("userRatingChangeFrequencyMultiplayer")]
    public required double UserRatingChangeFrequencyMultiplayer { get; set; }

    [JsonPropertyName("youSellOfferMaxStorageTimeInHour")]
    public required double YouSellOfferMaxStorageTimeInHour { get; set; }

    [JsonPropertyName("yourOfferDidNotSellMaxStorageTimeInHour")]
    public required double YourOfferDidNotSellMaxStorageTimeInHour { get; set; }
}

public record RagFairItemRestriction
{
    public required double MaxFlea { get; set; }

    public required double MaxFleaStacked { get; set; }

    public required string TemplateId { get; set; }
}

public record RagFairActiveOfferCount
{
    [JsonPropertyName("count")]
    public required int Count { get; set; }

    [JsonPropertyName("countForSpecialEditions")]
    public required int CountForSpecialEditions { get; set; }

    [JsonPropertyName("from")]
    public required double From { get; set; }

    [JsonPropertyName("to")]
    public required double To { get; set; }
}

public record RagFairRarityValue
{
    [JsonPropertyName("value")]
    public required double Value { get; set; }
}
