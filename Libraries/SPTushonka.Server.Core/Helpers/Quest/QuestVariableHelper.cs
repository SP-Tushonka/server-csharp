using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTarkov.Server.Core.Helpers.Quest;

/// <summary>
///     Keeps a profile's variable group members in step with its completed quests. Live sets one member per
///     completed quest and the client sums a group to gate the trader side quests and the story endings, so
///     without this every gated quest stays locked.
/// </summary>
[Injectable]
public class QuestVariableHelper(TemplateTable templateTable)
{
    /// <summary>Set the members a just completed quest earns and report them to the client.</summary>
    public void RecordCompletion(PmcData pmcData, MongoId questId, ProfileChange? changes)
    {
        foreach (
            var group in templateTable.QuestVariables.Values.Where(group =>
                group.Quests.ContainsKey(questId) || group.Fillers.Any(fillers => fillers.Quests.Contains(questId))
            )
        )
        {
            Sync(pmcData, group, changes);
        }
    }

    /// <summary>Bring every group up to the profile's completed quests. Never clears a member, so it is safe on every login.</summary>
    public void SyncAll(PmcData pmcData)
    {
        foreach (var group in templateTable.QuestVariables.Values)
        {
            Sync(pmcData, group, null);
        }
    }

    /// <summary>
    ///     Sets the group's flags for the quests the profile has completed. A quest with a known flag sets that
    ///     flag, the other quests share a pool of leftover flags and set one each, so the group's total matches live.
    /// </summary>
    private void Sync(PmcData pmcData, QuestVariableGroup group, ProfileChange? changes)
    {
        var members = templateTable.VariableGroups.FirstOrDefault(entry => entry.Id == group.Id)?.Variables;
        if (members is null)
        {
            return;
        }

        var completed =
            pmcData.Quests?.Where(quest => quest.Status == QuestStatusEnum.Success).Select(quest => quest.QId).ToHashSet() ?? [];
        pmcData.Variables ??= [];

        foreach (var (quest, member) in group.Quests.Where(pair => completed.Contains(pair.Key)))
        {
            Set(pmcData, member, changes);
        }

        var owned = group.Quests.Values.ToHashSet();
        var free = members.Where(member => !owned.Contains(member)).ToList();
        var completedFillers = group.Fillers.Sum(fillers => FilledMembers(fillers, completed));
        var shortfall = Math.Min(completedFillers, free.Count) - free.Count(member => pmcData.Variables.GetValueOrDefault(member) != 0);
        foreach (var member in free.Where(member => pmcData.Variables.GetValueOrDefault(member) == 0).Take(Math.Max(shortfall, 0)))
        {
            Set(pmcData, member, changes);
        }
    }

    // A class with fewer quests than members is set as a whole by any of them, the Tour start sets four at once
    private static int FilledMembers(QuestVariableFillers fillers, HashSet<MongoId> completed)
    {
        var done = fillers.Quests.Count(completed.Contains);
        if (fillers.Quests.Count < fillers.Members)
        {
            return done > 0 ? fillers.Members : 0;
        }

        return Math.Min(fillers.Members, done);
    }

    private static void Set(PmcData pmcData, MongoId member, ProfileChange? changes)
    {
        if (pmcData.Variables!.GetValueOrDefault(member) != 0)
        {
            return;
        }

        pmcData.Variables[member] = 1;
        if (changes is not null)
        {
            changes.VariableValues ??= [];
            changes.VariableValues[member] = 1;
        }
    }
}
