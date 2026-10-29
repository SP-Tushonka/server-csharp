using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Extensions;

public static class QuestConditionExtensions
{
    /// <summary>
    /// Get all quest conditions from provided list
    /// </summary>
    /// <param name="questConditions">Input conditions</param>
    /// <param name="furtherFilter">OPTIONAL - Additional filter code to run</param>
    /// <returns></returns>
    public static List<QuestCondition> GetQuestConditions(
        this IEnumerable<QuestCondition> questConditions,
        Func<QuestCondition, List<QuestCondition>>? furtherFilter = null
    )
    {
        return FilterConditions(questConditions, "Quest", furtherFilter);
    }

    public static List<QuestCondition> GetLevelConditions(
        this IEnumerable<QuestCondition> questConditions,
        Func<QuestCondition, List<QuestCondition>>? furtherFilter = null
    )
    {
        return FilterConditions(questConditions, "Level", furtherFilter);
    }

    public static List<QuestCondition> GetLoyaltyConditions(
        this IEnumerable<QuestCondition> questConditions,
        Func<QuestCondition, List<QuestCondition>>? furtherFilter = null
    )
    {
        return FilterConditions(questConditions, "TraderLoyalty", furtherFilter);
    }

    public static List<QuestCondition> GetStandingConditions(
        this IEnumerable<QuestCondition> questConditions,
        Func<QuestCondition, List<QuestCondition>>? furtherFilter = null
    )
    {
        return FilterConditions(questConditions, "TraderStanding", furtherFilter);
    }

    /// <summary>
    ///     Get the quests named by the Quest conditions in a list, in order
    /// </summary>
    /// <param name="questConditions">Conditions to read</param>
    /// <returns>Quest ids</returns>
    public static List<MongoId> GetQuestTargets(this IEnumerable<QuestCondition> questConditions)
    {
        return questConditions
            .Where(condition => condition.ConditionType == "Quest" && condition.Target?.Item is not null)
            .Select(condition => new MongoId(condition.Target!.Item!))
            .ToList();
    }

    /// <summary>
    ///     Check whether a condition is a Quest condition on the given quest
    /// </summary>
    /// <param name="condition">Condition to check</param>
    /// <param name="questId">Quest it may name</param>
    /// <returns>True when the condition names the quest</returns>
    public static bool IsQuestConditionOn(this QuestCondition condition, MongoId questId)
    {
        return condition.ConditionType == "Quest" && condition.Target?.Item is not null && new MongoId(condition.Target.Item) == questId;
    }

    private static List<QuestCondition> FilterConditions(
        IEnumerable<QuestCondition> questConditions,
        string questType,
        Func<QuestCondition, List<QuestCondition>>? furtherFilter = null
    )
    {
        var filteredQuests = questConditions
            .Where(c =>
            {
                if (c.ConditionType == questType)
                // return true or run the passed in function
                {
                    return furtherFilter is null || furtherFilter(c).Any();
                }

                return false;
            })
            .ToList();

        return filteredQuests;
    }
}
