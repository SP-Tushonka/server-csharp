using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

public record ProfileBattlePassProgress
{
    [JsonPropertyName("battlePassId")]
    public MongoId BattlePassId { get; set; }

    [JsonPropertyName("completed")]
    public int? Completed { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("obtainedRewardIds")]
    public List<MongoId>? ObtainedRewardIds { get; set; }
}

public record ProfileBattlePassDocumentLimit
{
    [JsonPropertyName("nextResetTime")]
    public long? NextResetTime { get; set; }

    [JsonPropertyName("remainingLimit")]
    public int? RemainingLimit { get; set; }

    [JsonPropertyName("totalLimit")]
    public int? TotalLimit { get; set; }

    [JsonPropertyName("resetInterval")]
    public int? ResetInterval { get; set; }
}

public record ProfileEnding
{
    [JsonPropertyName("current")]
    public string? Current { get; set; }

    [JsonPropertyName("achieved")]
    public List<string>? Achieved { get; set; }
}
