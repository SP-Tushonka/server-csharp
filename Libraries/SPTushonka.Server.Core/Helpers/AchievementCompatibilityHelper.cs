using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Helpers;

/// <summary>
///     Builds a client-compatible achievement list without changing catalog or profile state.
/// </summary>
public static class AchievementCompatibilityHelper
{
    public static List<Achievement> FilterUnsupported(
        IEnumerable<Achievement> achievements,
        IEnumerable<string>? unsupportedConditionTypes
    )
    {
        var unsupported = new HashSet<string>(unsupportedConditionTypes ?? [], StringComparer.Ordinal);
        return achievements.Where(achievement => !ContainsUnsupported(achievement.Conditions, unsupported)).ToList();
    }

    private static bool ContainsUnsupported(AchievementQuestConditionTypes? conditions, HashSet<string> unsupported)
    {
        if (conditions is null || unsupported.Count == 0)
        {
            return false;
        }

        IEnumerable<QuestCondition>?[] groups =
        [conditions.Started, conditions.AvailableForFinish, conditions.AvailableForStart, conditions.Success, conditions.Fail];

        return groups.Any(group => group?.Any(condition =>
            unsupported.Contains(condition.ConditionType)
            || (condition.Counter?.Conditions?.Any(child => unsupported.Contains(child.ConditionType)) ?? false)
            || (condition.VisibilityConditions?.Any(child => unsupported.Contains(child.ConditionType)) ?? false)
        ) ?? false);
    }
}
