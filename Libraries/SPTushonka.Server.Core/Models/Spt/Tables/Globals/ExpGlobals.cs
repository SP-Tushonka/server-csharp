using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Spt.Tables.Globals;

public record ExpGlobals
{
    [JsonPropertyName("ExpByGameEditionMultiplier")]
    public required List<ExperienceByGameEditionMultiplier> ExperienceByGameEditionMultiplier { get; set; }

    [JsonPropertyName("expForLevelOneDogtag")]
    public required double ExperienceForLevelOneDogtag { get; set; }

    [JsonPropertyName("expForLockedDoorBreach")]
    public required double ExperienceForLockedDoorBreach { get; set; }

    [JsonPropertyName("expForLockedDoorOpen")]
    public required double ExperienceForLockedDoorOpen { get; set; }

    [JsonPropertyName("heal")]
    public required ExpHeal Heal { get; set; }

    [JsonPropertyName("kill")]
    public required ExpKill Kill { get; set; }

    [JsonPropertyName("level")]
    public required ExperienceLevel Level { get; set; }

    [JsonPropertyName("loot_attempts")]
    public required IEnumerable<LootAttempt> LootAttempts { get; set; }

    [JsonPropertyName("match_end")]
    public required MatchEndExp MatchEnd { get; set; }

    [JsonPropertyName("triggerMult")]
    public required double TriggerMult { get; set; }
}

public record ExperienceByGameEditionMultiplier
{
    public required string GameEdition { get; set; }

    public required double SelfExpMultiplier { get; set; }

    public required double TeamExpMultiplier { get; set; }
}

public record ExpHeal
{
    [JsonPropertyName("expForEnergy")]
    public required double ExperienceForEnergy { get; set; }

    [JsonPropertyName("expForHeal")]
    public required double ExperienceForHeal { get; set; }

    [JsonPropertyName("expForHydration")]
    public required double ExperienceForHydration { get; set; }
}

public record ExpKill
{
    [JsonPropertyName("bloodLossToLitre")]
    public required double BloodLossToLitre { get; set; }

    [JsonPropertyName("botExpOnDamageAllHealth")]
    public required double BotExpOnDamageAllHealth { get; set; }

    [JsonPropertyName("botHeadShotMult")]
    public required double BotHeadShotMultiplier { get; set; }

    [JsonPropertyName("combo")]
    public required List<ComboExp> Combo { get; set; } = [];

    [JsonPropertyName("longShotDistance")]
    public required double LongShotDistance { get; set; }

    [JsonPropertyName("pmcExpOnDamageAllHealth")]
    public required double PmcExpOnDamageAllHealth { get; set; }

    [JsonPropertyName("pmcHeadShotMult")]
    public required double PmcHeadShotMultiplier { get; set; }

    [JsonPropertyName("victimBotLevelExp")]
    public required double VictimBotLevelExperience { get; set; }

    [JsonPropertyName("victimLevelExp")]
    public required double VictimLevelExperience { get; set; }
}

public record ComboExp
{
    [JsonPropertyName("percent")]
    public required double Percent { get; set; }
}

public record ExperienceLevel
{
    [JsonPropertyName("clan_level")]
    public required int ClanLevel { get; set; }

    [JsonPropertyName("exp_table")]
    public required ExperienceTable[] ExperienceTable { get; set; } = [];

    [JsonPropertyName("mastering1")]
    public required double Mastering1 { get; set; }

    [JsonPropertyName("mastering2")]
    public required double Mastering2 { get; set; }

    [JsonPropertyName("savage_level")]
    public required int SavageLevel { get; set; }

    [JsonPropertyName("trade_level")]
    public required int TradeLevel { get; set; }
}

public record ExperienceTable
{
    [JsonPropertyName("exp")]
    public required int Experience { get; set; }
}

public record LootAttempt
{
    [JsonPropertyName("k_exp")]
    public required double KExp { get; set; }
}

public record MatchEndExp
{
    public required string README { get; set; }

    [JsonPropertyName("killedMult")]
    public required double KilledMultiplier { get; set; }

    [JsonPropertyName("leftMult")]
    public required double LeftMultiplier { get; set; }

    [JsonPropertyName("miaMult")]
    public required double MiaMultiplier { get; set; }

    [JsonPropertyName("mia_exp_reward")]
    public required double MiaExpReward { get; set; }

    [JsonPropertyName("runnerMult")]
    public required double RunnerMultiplier { get; set; }

    [JsonPropertyName("runner_exp_reward")]
    public required double RunnerExpReward { get; set; }

    [JsonPropertyName("survivedMult")]
    public required double SurvivedMultiplier { get; set; }

    [JsonPropertyName("survived_exp_requirement")]
    public required double SurvivedExpRequirement { get; set; }

    [JsonPropertyName("survived_exp_reward")]
    public required double SurvivedExpReward { get; set; }

    [JsonPropertyName("survived_seconds_requirement")]
    public required double SurvivedSecondsRequirement { get; set; }

    [JsonPropertyName("transit_exp_reward")]
    public required double TransitExpReward { get; set; }

    [JsonPropertyName("transit_mult")]
    public required TransitMultiplier[] TransitMultiplier { get; set; }
}

public record TransitMultiplier
{
    [JsonPropertyName("mlp")]
    public required double Multipllier { get; set; }
}
