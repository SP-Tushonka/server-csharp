using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Models.Eft.BattlePass;

public record BattlePassAssortOffer
{
    [JsonPropertyName("rewardId")]
    public MongoId RewardId { get; set; }

    [JsonPropertyName("barter_scheme")]
    public List<List<BarterScheme>>? BarterScheme { get; set; }

    [JsonPropertyName("loyaltyLevel")]
    public int? LoyaltyLevel { get; set; }
}
