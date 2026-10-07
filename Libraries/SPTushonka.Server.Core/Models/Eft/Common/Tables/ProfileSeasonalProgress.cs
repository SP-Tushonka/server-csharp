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

    /// <summary>
    ///     Endings open to the character, keyed by mode. Live sends regular and pve, both empty
    /// </summary>
    [JsonPropertyName("available")]
    public Dictionary<string, List<string>>? Available { get; set; }
}

/// <summary>
///     The 1.2 operator and candidate account link. Live sends no operator and no candidates
/// </summary>
public record ProfilePdt
{
    [JsonPropertyName("OperatorAccountId")]
    public string? OperatorAccountId { get; set; }

    [JsonPropertyName("CandidateAccountIds")]
    public List<string> CandidateAccountIds { get; set; } = [];
}
