using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Models.Eft.Seasons;

/// <summary>
///     Response of /client/season/active.
/// </summary>
public record SeasonActiveResponse
{
    [JsonPropertyName("season")]
    public Season? Season { get; set; }
}

public record Season
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("startTs")]
    public long? StartTs { get; set; }

    [JsonPropertyName("endTs")]
    public long? EndTs { get; set; }

    [JsonPropertyName("seasonalRewards")]
    public List<SeasonalReward>? SeasonalRewards { get; set; }
}

public record SeasonalReward
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("seasonId")]
    public MongoId SeasonId { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("bigImageUrl")]
    public string? BigImageUrl { get; set; }

    /// <summary>Placement on the seasonal reward grid.</summary>
    [JsonPropertyName("location")]
    public RewardGridLocation? Location { get; set; }

    [JsonPropertyName("conditions")]
    public List<QuestCondition>? Conditions { get; set; }

    [JsonPropertyName("rewards")]
    public List<Reward>? Rewards { get; set; }
}

public record RewardGridLocation
{
    [JsonPropertyName("x")]
    public int? X { get; set; }

    [JsonPropertyName("y")]
    public int? Y { get; set; }

    [JsonPropertyName("w")]
    public int? W { get; set; }

    [JsonPropertyName("h")]
    public int? H { get; set; }
}
