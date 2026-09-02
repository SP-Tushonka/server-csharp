using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Enums;

namespace SPTarkov.Server.Core.Models.Eft.Profile;

public record CharacterSelectionProfileData
{
    [JsonPropertyName("uid")]
    public MongoId? Uid { get; set; }

    [JsonPropertyName("aid")]
    public int? Aid { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("lowerNickname")]
    public string? LowerNickname { get; set; }

    [JsonPropertyName("nicknamePref")]
    public string? NicknamePref { get; set; }

    [JsonPropertyName("side")]
    public string? Side { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("prestigeLevel")]
    public int? PrestigeLevel { get; set; }

    [JsonPropertyName("gameVersion")]
    public string? GameVersion { get; set; }

    [JsonPropertyName("memberCategory")]
    public MemberCategory? MemberCategory { get; set; }

    [JsonPropertyName("accountType")]
    public int? AccountType { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("SeasonalInfo")]
    public CharacterSelectionSeasonalInfo? SeasonalInfo { get; set; }

    [JsonPropertyName("BattlePassProgress")]
    public List<BattlePassProgress>? BattlePassProgress { get; set; }

    [JsonPropertyName("PlayerVisualRepresentation")]
    public PlayerVisualRepresentation? PlayerVisualRepresentation { get; set; }

    [JsonPropertyName("unlockedTraders")]
    public List<MongoId>? UnlockedTraders { get; set; }

    [JsonPropertyName("unlockedRules")]
    public List<MongoId>? UnlockedRules { get; set; }

    [JsonPropertyName("unlockedTraderDialogues")]
    public List<MongoId>? UnlockedTraderDialogues { get; set; }

    [JsonPropertyName("unlockedProductionRecipe")]
    public List<MongoId>? UnlockedProductionRecipe { get; set; }
}

public record BattlePassProgress
{
    [JsonPropertyName("battlePassId")]
    public MongoId BattlePassId { get; set; }

    [JsonPropertyName("completed")]
    public int? Completed { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }
}

public record CharacterSelectionSeasonalInfo
{
    [JsonPropertyName("seasonNameLocalizationKey")]
    public string? SeasonNameLocalizationKey { get; set; }

    [JsonPropertyName("seasonEndAt")]
    public string? SeasonEndAt { get; set; }

    [JsonPropertyName("kdPmc")]
    public double? KdPmc { get; set; }

    [JsonPropertyName("raidsSurvivedPmc")]
    public int? RaidsSurvivedPmc { get; set; }

    [JsonPropertyName("selectedSeasonalPerkIds")]
    public List<MongoId>? SelectedSeasonalPerkIds { get; set; }

    [JsonPropertyName("battlePassRewards")]
    public List<BattlePassProgress>? BattlePassRewards { get; set; }

    [JsonPropertyName("achievements")]
    public CharacterSelectionAchievements? Achievements { get; set; }

    [JsonPropertyName("storyChapters")]
    public CharacterSelectionStoryChapters? StoryChapters { get; set; }
}

public record CharacterSelectionAchievements
{
    [JsonPropertyName("completedAchievementIds")]
    public List<MongoId>? CompletedAchievementIds { get; set; }
}

public record CharacterSelectionStoryChapters
{
    [JsonPropertyName("completed")]
    public int? Completed { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }
}
