using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Seasons;

namespace SPTarkov.Server.Core.Models.Eft.Leagues;

/// <summary>
///     One element of /client/leagues/ranks, the reward grid of a league rank.
/// </summary>
public record LeagueRank
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("picture")]
    public required string Picture { get; set; }

    [JsonPropertyName("rewards")]
    public required List<LeagueRankReward> Rewards { get; set; }
}

public record LeagueRankReward
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("reward")]
    public required Reward Reward { get; set; }

    [JsonPropertyName("imageUrl")]
    public required string ImageUrl { get; set; }

    [JsonPropertyName("location")]
    public required RewardGridLocation Location { get; set; }
}
