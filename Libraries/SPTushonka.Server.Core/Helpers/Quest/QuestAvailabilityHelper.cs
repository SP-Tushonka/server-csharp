using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Server;

namespace SPTarkov.Server.Core.Helpers.Quest;

/// <summary>
///     Checks that decide whether a player may see a quest, by level, trader, side, game version and seasonal event
/// </summary>
[Injectable]
public class QuestAvailabilityHelper(
    ISptLogger<QuestAvailabilityHelper> logger,
    ServerLocalisationService serverLocalisationService,
    SeasonalEventService seasonalEventService,
    QuestConfig questConfig
)
{
    /// <summary>
    ///     returns true if the level condition is satisfied
    /// </summary>
    /// <param name="playerLevel">Players level</param>
    /// <param name="condition">Quest condition</param>
    /// <returns>true if player level is greater than or equal to quest</returns>
    public bool DoesPlayerLevelFulfilCondition(int playerLevel, QuestCondition condition)
    {
        if (condition.ConditionType != "Level")
        {
            return true;
        }

        var conditionValue = condition.Value;
        switch (condition.CompareMethod)
        {
            case ">=":
                return playerLevel >= conditionValue;
            case ">":
                return playerLevel > conditionValue;
            case "<":
                return playerLevel < conditionValue;
            case "<=":
                return playerLevel <= conditionValue;
            case "=":
                return conditionValue.Approx(playerLevel);
            default:
                logger.Error(serverLocalisationService.GetText("quest-unable_to_find_compare_condition", condition.CompareMethod));

                return false;
        }
    }

    /// <summary>
    /// Does a provided quest have a level requirement equal to or below defined level
    /// </summary>
    /// <param name="quest">Quest to check</param>
    /// <param name="playerLevel">level of player to test against quest</param>
    /// <returns>true if quest can be seen/accepted by player of defined level</returns>
    public bool PlayerLevelFulfillsQuestRequirement(Models.Eft.Common.Tables.Quest quest, int playerLevel)
    {
        var levelConditions = quest.Conditions.AvailableForStart?.GetLevelConditions();
        if (levelConditions is not null)
        {
            foreach (var levelCondition in levelConditions)
            {
                if (!DoesPlayerLevelFulfilCondition(playerLevel, levelCondition))
                // Not valid, exit out
                {
                    return false;
                }
            }
        }

        // All conditions passed / has no level requirement, valid
        return true;
    }

    /// <summary>
    ///     Check if trader has sufficient loyalty to fulfill quest requirement
    /// </summary>
    /// <param name="questProperties">Quest props</param>
    /// <param name="profile">Player profile</param>
    /// <returns>true if loyalty is high enough to fulfill quest requirement</returns>
    public bool TraderLoyaltyLevelRequirementCheck(QuestCondition questProperties, PmcData profile)
    {
        if (
            !profile.TradersInfo!.TryGetValue(
                questProperties.Target!.IsItem ? questProperties.Target.Item! : questProperties.Target.List!.FirstOrDefault()!,
                out var trader
            )
        )
        {
            logger.Error(serverLocalisationService.GetText("quest-unable_to_find_trader_in_profile", questProperties.Target));

            return false;
        }

        return CompareAvailableForValues(trader.LoyaltyLevel!.Value, questProperties.Value!.Value, questProperties.CompareMethod!);
    }

    /// <summary>
    ///     Check if trader has sufficient standing to fulfill quest requirement
    /// </summary>
    /// <param name="questProperties">Quest props</param>
    /// <param name="profile">Player profile</param>
    /// <returns>true if standing is high enough to fulfill quest requirement</returns>
    public bool TraderStandingRequirementCheck(QuestCondition questProperties, PmcData profile)
    {
        var requiredLoyaltyLevel = Convert.ToInt32(questProperties.Value);
        if (
            !profile.TradersInfo!.TryGetValue(
                questProperties.Target!.IsItem ? questProperties.Target.Item! : questProperties.Target.List!.FirstOrDefault()!,
                out var trader
            )
        )
        {
            logger.Error(serverLocalisationService.GetText("quest-unable_to_find_trader_in_profile", questProperties.Target));
        }

        return CompareAvailableForValues(trader?.Standing ?? 1, requiredLoyaltyLevel, questProperties.CompareMethod!);
    }

    /// <summary>
    /// Should a seasonal/event quest be shown to the player
    /// </summary>
    /// <param name="questId">Quest to check</param>
    /// <returns>true = show to player</returns>
    public bool ShowEventQuestToPlayer(MongoId questId)
    {
        var isChristmasEventActive = seasonalEventService.ChristmasEventEnabled();
        var isHalloweenEventActive = seasonalEventService.HalloweenEventEnabled();

        // Not christmas + quest is for christmas
        if (!isChristmasEventActive && seasonalEventService.IsQuestRelatedToEvent(questId, SeasonalEventType.Christmas))
        {
            return false;
        }

        // Not halloween + quest is for halloween
        if (!isHalloweenEventActive && seasonalEventService.IsQuestRelatedToEvent(questId, SeasonalEventType.Halloween))
        {
            return false;
        }

        if (!seasonalEventService.ScourgeEventEnabled() && seasonalEventService.IsQuestRelatedToEvent(questId, SeasonalEventType.Scourge))
        {
            return false;
        }

        // Should non-season event quests be shown to player
        if (!questConfig.ShowNonSeasonalEventQuests && seasonalEventService.IsQuestRelatedToEvent(questId, SeasonalEventType.None))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Is the quest for the opposite side the player is on
    /// </summary>
    /// <param name="playerSide">Player side (usec/bear)</param>
    /// <param name="questId">QuestId to check</param>
    /// <returns>true = quest isn't for player</returns>
    public bool QuestIsForOtherSide(string? playerSide, MongoId questId)
    {
        var isUsec = string.Equals(playerSide, "usec", StringComparison.OrdinalIgnoreCase);
        if (isUsec && questConfig.BearOnlyQuests.Contains(questId))
        // Player is usec and quest is bear only, skip
        {
            return true;
        }

        if (!isUsec && questConfig.UsecOnlyQuests.Contains(questId))
        // Player is bear and quest is usec only, skip
        {
            return true;
        }

        // player is bear + quest is usec OR player is usec + quest is bear
        return false;
    }

    /// <summary>
    /// Is the provided quest prevented from being viewed by the provided game version
    /// (Inclusive filter)
    /// </summary>
    /// <param name="gameVersion">Game version to check against</param>
    /// <param name="questId">Quest id to check</param>
    /// <returns>True = Quest should not be visible to game version</returns>
    public bool QuestIsProfileBlacklisted(string? gameVersion, MongoId questId)
    {
        if (!questConfig.ProfileBlacklist.TryGetValue(gameVersion ?? string.Empty, out var blacklistedQuests))
        {
            // Game version has no blacklist
            return false;
        }

        return blacklistedQuests.Contains(questId);
    }

    /// <summary>
    /// Is the provided quest able to be seen by the provided game version
    /// (Exclusive filter)
    /// </summary>
    /// <param name="gameVersion">Game version to check against</param>
    /// <param name="questId">Quest id to check</param>
    /// <returns>True = Quest should be visible to game version</returns>
    public bool QuestIsProfileWhitelisted(string? gameVersion, MongoId questId)
    {
        if (!questConfig.ProfileWhitelist.TryGetValue(questId, out var allowedGameVersions))
        {
            // Quest isn't whitelist restricted, visible to all game versions
            return true;
        }

        return allowedGameVersions.Contains(gameVersion ?? string.Empty);
    }

    /// <summary>
    /// Helper to map symbols to actions
    /// </summary>
    /// <param name="current">First value</param>
    /// <param name="required">Second value</param>
    /// <param name="compareMethod">Symbol to compare two values with e.g. ">="</param>
    /// <returns>Outcome of comparison</returns>
    protected bool CompareAvailableForValues(double current, double required, string compareMethod)
    {
        switch (compareMethod)
        {
            case ">=":
                return current >= required;
            case ">":
                return current > required;
            case "<=":
                return current <= required;
            case "<":
                return current < required;
            case "!=":
                return !current.Approx(required);
            case "==":
                return current.Approx(required);

            default:
                logger.Error(serverLocalisationService.GetText("quest-compare_operator_unhandled", compareMethod));

                return false;
        }
    }
}
