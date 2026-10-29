using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Extensions;

public static class QuestExtensions
{
    /// <summary>
    ///     Check whether the player has read every note, tape or patch a quest's start conditions ask for
    /// </summary>
    /// <param name="quest">Quest to check</param>
    /// <param name="profile">Player profile</param>
    /// <returns>True when every CompletableItem start condition holds</returns>
    public static bool CompletableItemRequirementsMet(this Quest quest, PmcData profile)
    {
        return (quest.Conditions?.AvailableForStart ?? [])
            .Where(condition => condition.ConditionType == "CompletableItem" && condition.Target?.Item is not null)
            .All(condition => profile.CompletableItems?.GetValueOrDefault(condition.Target!.Item!) ?? false);
    }

    /// <summary>
    ///     Check whether the player has read the items that start a quest by themselves
    /// </summary>
    /// <param name="quest">Quest to check</param>
    /// <param name="profile">Player profile</param>
    /// <returns>True when the quest has CompletableItem auto start conditions and all of them hold</returns>
    public static bool AutoStartConditionsMet(this Quest quest, PmcData profile)
    {
        var conditions = (quest.Conditions?.AutoStart ?? [])
            .Where(condition => condition.ConditionType == "CompletableItem" && condition.Target?.Item is not null)
            .ToList();

        return conditions.Count > 0
            && conditions.All(condition => profile.CompletableItems?.GetValueOrDefault(condition.Target!.Item!) ?? false);
    }

    /// <summary>
    ///     Check the prestige level a quest asks for, such as the New Beginning quest each prestige level requires
    /// </summary>
    /// <param name="quest">Quest to check</param>
    /// <param name="profile">Player profile</param>
    /// <returns>True when every PrestigeLevel start condition holds</returns>
    public static bool PrestigeLevelRequirementsMet(this Quest quest, PmcData profile)
    {
        // Every prestige on this server is PvE
        var prestigeLevel = profile.Info?.GetPrestigeLevel(PrestigeGameModes.Pve) ?? 0;

        return (quest.Conditions?.AvailableForStart ?? [])
            .Where(condition => condition.ConditionType == "PrestigeLevel")
            .All(condition =>
                condition.CompareMethod switch
                {
                    ">=" => prestigeLevel >= condition.Value,
                    ">" => prestigeLevel > condition.Value,
                    "<=" => prestigeLevel <= condition.Value,
                    "<" => prestigeLevel < condition.Value,
                    _ => prestigeLevel == condition.Value,
                }
            );
    }
}
