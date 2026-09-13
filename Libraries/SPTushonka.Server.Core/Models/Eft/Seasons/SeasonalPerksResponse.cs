using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Seasons;

/// <summary>
///     Response of /client/seasonal-perks/list.
/// </summary>
public record SeasonalPerksResponse
{
    [JsonPropertyName("common")]
    public required List<SeasonalPerk> Common { get; set; }

    [JsonPropertyName("personal")]
    public required List<SeasonalPerk> Personal { get; set; }
}

public record SeasonalPerk
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("imageUrl")]
    public required string ImageUrl { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("effects")]
    public required List<SeasonalPerkEffect> Effects { get; set; }

    [JsonPropertyName("mutuallyExclusiveSeasonalPerkIds")]
    public required List<MongoId> MutuallyExclusiveSeasonalPerkIds { get; set; }
}

public record SeasonalPerkEffect
{
    [JsonPropertyName("effectId")]
    public required string EffectId { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("intValue")]
    public int? IntValue { get; set; }

    [JsonPropertyName("multiplicator")]
    public double? Multiplicator { get; set; }

    [JsonPropertyName("multiplicatorPrimary")]
    public double? MultiplicatorPrimary { get; set; }

    [JsonPropertyName("multiplicatorSecondary")]
    public double? MultiplicatorSecondary { get; set; }

    [JsonPropertyName("skillIds")]
    public List<string>? SkillIds { get; set; }

    [JsonPropertyName("bodyPartTypes")]
    public List<string>? BodyPartTypes { get; set; }

    [JsonPropertyName("keyTypes")]
    public List<string>? KeyTypes { get; set; }

    [JsonPropertyName("traderIds")]
    public List<MongoId>? TraderIds { get; set; }

    [JsonPropertyName("tradeAction")]
    public string? TradeAction { get; set; }

    [JsonPropertyName("mailTemplateId")]
    public MongoId? MailTemplateId { get; set; }

    [JsonPropertyName("periodUnixSeconds")]
    public long? PeriodUnixSeconds { get; set; }

    [JsonPropertyName("randomSlotCount")]
    public int? RandomSlotCount { get; set; }

    [JsonPropertyName("appliedRandomEffectCount")]
    public int? AppliedRandomEffectCount { get; set; }

    [JsonPropertyName("itemFilter")]
    public SeasonalPerkItemFilter? ItemFilter { get; set; }

    [JsonPropertyName("subEffects")]
    public SeasonalPerkSubEffects? SubEffects { get; set; }
}

public record SeasonalPerkItemFilter
{
    [JsonPropertyName("include")]
    public required List<SeasonalPerkFilterEntry> Include { get; set; }

    [JsonPropertyName("exclude")]
    public required List<SeasonalPerkFilterEntry> Exclude { get; set; }
}

public record SeasonalPerkFilterEntry
{
    [JsonPropertyName("field")]
    public required string Field { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

public record SeasonalPerkSubEffects
{
    [JsonPropertyName("energyRecovery")]
    public required SeasonalPerkSubEffect EnergyRecovery { get; set; }

    [JsonPropertyName("healthRegeneration")]
    public required SeasonalPerkSubEffect HealthRegeneration { get; set; }

    [JsonPropertyName("hydrationRecovery")]
    public required SeasonalPerkSubEffect HydrationRecovery { get; set; }

    [JsonPropertyName("onPainkillers")]
    public required SeasonalPerkSubEffect OnPainkillers { get; set; }

    [JsonPropertyName("pain")]
    public required SeasonalPerkSubEffect Pain { get; set; }

    [JsonPropertyName("tremor")]
    public required SeasonalPerkSubEffect Tremor { get; set; }

    [JsonPropertyName("tunnelVision")]
    public required SeasonalPerkSubEffect TunnelVision { get; set; }
}

public record SeasonalPerkSubEffect
{
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; set; }

    [JsonPropertyName("durationSeconds")]
    public required int DurationSeconds { get; set; }

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }
}
