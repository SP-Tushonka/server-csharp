using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;

namespace SPTarkov.Server.Core.Models.Eft.BattlePass;

/// <summary>
///     Sent when a reward is claimed from the season pass.
/// </summary>
public record BattlePassUnlockRewardRequest : BaseInteractionRequestData
{
    [JsonPropertyName("rewardId")]
    public MongoId RewardId { get; set; }

    [JsonPropertyName("battlePassId")]
    public MongoId BattlePassId { get; set; }

    /// <summary>Documents handed in from the stash to cover the reward's cost</summary>
    [JsonPropertyName("items")]
    public List<BattlePassHandIn>? Items { get; set; }

    /// <summary>What the rest of the cost is topped up with, taken from the profile's document balance.</summary>
    [JsonPropertyName("universalDocuments")]
    public int UniversalDocuments { get; set; }
}

public record BattlePassHandIn
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
