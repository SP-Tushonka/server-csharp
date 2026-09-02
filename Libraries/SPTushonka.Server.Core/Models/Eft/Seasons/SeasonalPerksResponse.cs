using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Seasons;

/// <summary>
///     Response of /client/seasonal-perks/list.
/// </summary>
public record SeasonalPerksResponse
{
    [JsonPropertyName("common")]
    public List<SeasonalPerk>? Common { get; set; }

    [JsonPropertyName("personal")]
    public List<SeasonalPerk>? Personal { get; set; }
}

public record SeasonalPerk
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("effects")]
    public List<SeasonalPerkEffect>? Effects { get; set; }

    [JsonPropertyName("mutuallyExclusiveSeasonalPerkIds")]
    public List<MongoId>? MutuallyExclusiveSeasonalPerkIds { get; set; }
}

public record SeasonalPerkEffect
{
    [JsonPropertyName("effectId")]
    public string? EffectId { get; set; }

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
    public List<SeasonalPerkFilterEntry>? Include { get; set; }

    [JsonPropertyName("exclude")]
    public List<SeasonalPerkFilterEntry>? Exclude { get; set; }
}

public record SeasonalPerkFilterEntry
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public record SeasonalPerkSubEffects
{
    [JsonPropertyName("energyRecovery")]
    public SeasonalPerkSubEffect? EnergyRecovery { get; set; }

    [JsonPropertyName("healthRegeneration")]
    public SeasonalPerkSubEffect? HealthRegeneration { get; set; }

    [JsonPropertyName("hydrationRecovery")]
    public SeasonalPerkSubEffect? HydrationRecovery { get; set; }

    [JsonPropertyName("onPainkillers")]
    public SeasonalPerkSubEffect? OnPainkillers { get; set; }

    [JsonPropertyName("pain")]
    public SeasonalPerkSubEffect? Pain { get; set; }

    [JsonPropertyName("tremor")]
    public SeasonalPerkSubEffect? Tremor { get; set; }

    [JsonPropertyName("tunnelVision")]
    public SeasonalPerkSubEffect? TunnelVision { get; set; }
}

public record SeasonalPerkSubEffect
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("durationSeconds")]
    public int? DurationSeconds { get; set; }

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }
}
