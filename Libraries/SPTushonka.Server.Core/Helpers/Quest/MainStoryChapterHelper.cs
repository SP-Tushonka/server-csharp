using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Helpers.Quest;

/// <summary>
///     Rules of the story chapters named by the main quest notes. A chapter lists its tasks as finish conditions in
///     story order, and a step is a task plus the alternatives listed after it
/// </summary>
[Injectable(InjectionType.Singleton)]
public class MainStoryChapterHelper(TemplateTable templateTable, TimeUtil timeUtil, QuestConfig questConfig)
{
    private Dictionary<MongoId, MongoId>? _taskChapters;
    private Dictionary<MongoId, HashSet<MongoId>>? _chapterStarters;
    private HashSet<MongoId>? _dialogueAcceptedQuests;

    /// <summary>
    ///     Check whether a quest is a story chapter
    /// </summary>
    /// <param name="questId">Quest to check</param>
    /// <returns>True when a main quest note names the quest as its chapter</returns>
    public bool IsStoryChapter(MongoId questId)
    {
        return templateTable.MainQuestNotes.Any(note => note.ChapterId == questId);
    }

    /// <summary>
    ///     Get the chapter that lists a quest as one of its tasks
    /// </summary>
    /// <param name="taskId">Quest to look up</param>
    /// <returns>Chapter id, or null when the quest is no chapter task</returns>
    public MongoId? ChapterOf(MongoId taskId)
    {
        _taskChapters ??= BuildTaskChapters();

        // A conditional expression would turn the null into an empty MongoId through the string conversion
        if (!_taskChapters.TryGetValue(taskId, out var chapterId))
        {
            return null;
        }

        return chapterId;
    }

    /// <summary>
    ///     Get the tasks a chapter lists as its finish conditions, in order
    /// </summary>
    /// <param name="chapterId">Chapter to read</param>
    /// <returns>Task ids</returns>
    public List<MongoId> ChapterTasks(MongoId chapterId)
    {
        return (templateTable.Quests.GetValueOrDefault(chapterId)?.Conditions?.AvailableForFinish ?? []).GetQuestTargets();
    }

    /// <summary>
    ///     Check whether the database has a task and does not withhold it
    /// </summary>
    /// <param name="taskId">Task to check</param>
    /// <returns>True when the server can give the task</returns>
    public bool IsAvailableTask(MongoId taskId)
    {
        return templateTable.Quests.ContainsKey(taskId) && !questConfig.WithheldQuests.Contains(taskId);
    }

    /// <summary>
    ///     Check whether a hidden starter of the chapter is done. A hidden task that fails when its chapter is already
    ///     running is that chapter's starter, the visit to the Labyrinth transit or Batya's camp
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="chapterId">Chapter to check</param>
    /// <returns>True when a starter succeeded</returns>
    public bool StarterCompleted(PmcData profile, MongoId chapterId)
    {
        _chapterStarters ??= BuildChapterStarters();

        return _chapterStarters.TryGetValue(chapterId, out var starters)
            && (
                profile.Quests?.Any(profileQuest => starters.Contains(profileQuest.QId) && profileQuest.Status == QuestStatusEnum.Success)
                ?? false
            );
    }

    /// <summary>
    ///     Check whether the step listed before a task is done, or the chapter starter for the first task
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="chapterId">Chapter of the task</param>
    /// <param name="taskId">Task to check</param>
    /// <returns>True when the task may start</returns>
    public bool PredecessorCompleted(PmcData profile, MongoId chapterId, MongoId taskId)
    {
        var tasks = ChapterTasks(chapterId);
        var index = tasks.IndexOf(taskId);
        if (index < 0)
        {
            return false;
        }

        // A task the database lacks or withholds can never finish, so the step before it counts instead
        var previous = StepStart(tasks, index) - 1;
        while (previous >= 0 && !IsAvailableTask(tasks[previous]))
        {
            previous--;
        }

        return previous < 0 ? StarterCompleted(profile, chapterId) : StepDone(profile, tasks, previous);
    }

    /// <summary>
    ///     Check whether a chapter still has a task offered, running or ready to hand in, or has not closed a task yet
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="chapterId">Chapter to check</param>
    /// <param name="shownQuests">Quests the player is shown, with their resolved status</param>
    /// <returns>True while the chapter is not done</returns>
    public bool HasOpenTask(PmcData profile, MongoId chapterId, List<Models.Eft.Common.Tables.Quest> shownQuests)
    {
        // The final step closes the chapter even when a branch the player did not take is still running
        var tasks = ChapterTasks(chapterId);
        if (FinalStepDone(profile, tasks))
        {
            return false;
        }

        var anyClosed = false;
        foreach (var taskId in tasks.Where(IsAvailableTask))
        {
            var status = shownQuests.FirstOrDefault(quest => quest.Id == taskId)?.SptStatus ?? profile.GetQuestStatus(taskId);
            if (
                status
                    is QuestStatusEnum.AvailableForStart
                        or QuestStatusEnum.Started
                        or QuestStatusEnum.AvailableForFinish
                        or QuestStatusEnum.AvailableAfter
                || (status is not (QuestStatusEnum.Success or QuestStatusEnum.Fail) && TaskStillComing(profile, taskId))
            )
            {
                return true;
            }

            anyClosed |= status is QuestStatusEnum.Success or QuestStatusEnum.Fail;
        }

        return !anyClosed;
    }

    /// <summary>
    ///     Check whether a task will still start, its prerequisite quests are done or still running and no dialogue
    ///     variable it needs has already moved past the value it asks for
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="taskId">Chapter task to check</param>
    /// <returns>True when the task will still start</returns>
    public bool TaskStillComing(PmcData profile, MongoId taskId)
    {
        if (!templateTable.Quests.TryGetValue(taskId, out var task) || FailConditionsMet(profile, task))
        {
            return false;
        }

        var conditions = task.Conditions?.AvailableForStart ?? [];
        var prerequisites = conditions.Where(condition => condition.ConditionType == "Quest" && condition.Target?.Item is not null).ToList();
        if (prerequisites.Count == 0)
        {
            return false;
        }

        foreach (var prerequisite in prerequisites)
        {
            var status = profile.GetQuestStatus(new MongoId(prerequisite.Target!.Item!));
            var done = prerequisite.Status?.Contains(status) ?? false;
            var running = status is QuestStatusEnum.Started or QuestStatusEnum.AvailableForFinish or QuestStatusEnum.AvailableAfter;
            if (!done && !running)
            {
                return false;
            }
        }

        // Dialogue variables only grow, so an exact value already passed never comes back
        return !conditions.Any(condition =>
            condition.ConditionType == "GlobalVariableValue"
            && condition.Target?.Item is not null
            && condition.CompareMethod is "==" or "<="
            && GetVariableValue(profile, new MongoId(condition.Target.Item)) > (condition.Value ?? 0)
        );
    }

    /// <summary>
    ///     Check whether a quest is handed out by an AcceptQuest dialogue action
    /// </summary>
    /// <param name="questId">Quest to check</param>
    /// <returns>True when a trader dialogue gives the quest</returns>
    public bool AcceptedInDialogue(MongoId questId)
    {
        _dialogueAcceptedQuests ??= templateTable
            .Dialogue.Elements.SelectMany(element => element.Lines)
            .SelectMany(line => line.Actions)
            .Where(action => action.Type == "AcceptQuest" && action.QuestId is not null)
            .Select(action => action.QuestId!.Value)
            .ToHashSet();

        return _dialogueAcceptedQuests.Contains(questId);
    }

    /// <summary>
    ///     Check whether a running quest finishes once this task starts, The Unheard's wait for Elektronik ends when
    ///     the tech files task is accepted
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="taskId">Task to check</param>
    /// <returns>True when a running quest waits for the task to start</returns>
    public bool AwaitedByRunningTask(PmcData profile, MongoId taskId)
    {
        return (profile.Quests ?? []).Any(profileQuest =>
            profileQuest.Status is QuestStatusEnum.Started or QuestStatusEnum.AvailableForFinish
            && (templateTable.Quests.GetValueOrDefault(profileQuest.QId)?.Conditions?.AvailableForFinish ?? []).Any(condition =>
                condition.IsQuestConditionOn(taskId) && (condition.Status?.Contains(QuestStatusEnum.Started) ?? false)
            )
        );
    }

    /// <summary>
    ///     Get the running chapter tasks the completed task is an alternative to, such as keeping Blue Fire's fragment
    ///     while the hand over is still running. Alternatives sit next to each other and only the later one names the
    ///     earlier one in its fail conditions
    /// </summary>
    /// <param name="completedTaskId">Task that was just completed</param>
    /// <param name="profile">Player profile</param>
    /// <returns>Templates of the alternatives to fail</returns>
    public List<Models.Eft.Common.Tables.Quest> RunningAlternativesOf(MongoId completedTaskId, PmcData profile)
    {
        var chapterId = ChapterOf(completedTaskId);
        if (chapterId is null)
        {
            return [];
        }

        var tasks = ChapterTasks(chapterId.Value);
        var first = tasks.IndexOf(completedTaskId);
        var last = first;
        while (first > 0 && FailsOnSuccessOf(tasks[first], tasks[first - 1]))
        {
            first--;
        }

        while (last + 1 < tasks.Count && FailsOnSuccessOf(tasks[last + 1], tasks[last]))
        {
            last++;
        }

        return tasks
            .GetRange(first, last - first + 1)
            .Where(taskId => taskId != completedTaskId)
            .Where(taskId => !StartsOnlyWhileRunning(completedTaskId, taskId) && !StartsOnlyWhileRunning(taskId, completedTaskId))
            .Where(taskId => profile.GetQuestStatus(taskId) is QuestStatusEnum.Started or QuestStatusEnum.AvailableForFinish)
            .Select(taskId => templateTable.Quests.GetValueOrDefault(taskId))
            .Where(task => task is not null)
            .Select(task => task!)
            .ToList();
    }

    /// <summary>
    ///     Check whether a quest's Fail conditions on other quests or on variables already hold
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="quest">Quest to check</param>
    /// <returns>True when the quest would fail at once</returns>
    public bool FailConditionsMet(PmcData profile, Models.Eft.Common.Tables.Quest quest)
    {
        return (quest.Conditions?.Fail ?? []).Any(condition =>
            condition.Target?.Item is not null
            && condition.ConditionType switch
            {
                "Quest" => condition.Status?.Contains(profile.GetQuestStatus(new MongoId(condition.Target.Item))) ?? false,
                "GlobalVariableValue" => VariableConditionMet(profile, condition),
                _ => false,
            }
        );
    }

    /// <summary>
    ///     Check every GlobalVariableValue start condition of a quest
    /// </summary>
    /// <param name="quest">Quest to check</param>
    /// <param name="profile">Player profile</param>
    /// <returns>True when the story has reached every value the quest asks for</returns>
    public bool GlobalVariableRequirementsMet(Models.Eft.Common.Tables.Quest quest, PmcData profile)
    {
        return (quest.Conditions?.AvailableForStart ?? [])
            .Where(condition => condition.ConditionType == "GlobalVariableValue" && condition.Target?.Item is not null)
            .All(condition => VariableConditionMet(profile, condition));
    }

    /// <summary>
    ///     Check whether the profile meets the group value, trader standing, loyalty or quest progress a quest's
    ///     hidden gate in the quest config asks for. Every part a gate names must hold
    /// </summary>
    /// <param name="questId">Quest to check</param>
    /// <param name="profile">Player profile</param>
    /// <returns>True when the quest has no gate or the gate is open</returns>
    public bool HiddenGateMet(MongoId questId, PmcData? profile)
    {
        if (!questConfig.HiddenVariableGates.TryGetValue(questId, out var gate))
        {
            return true;
        }

        if (profile is null)
        {
            return false;
        }

        if (gate.Trader is not null)
        {
            if (profile.TradersInfo is null || !profile.TradersInfo.TryGetValue(gate.Trader.Value, out var traderInfo))
            {
                return false;
            }

            var traderMet = gate.Loyalty is not null
                ? (traderInfo.LoyaltyLevel ?? 1) >= gate.Loyalty.Value
                : (traderInfo.Standing ?? 0) >= (gate.Standing ?? 0);
            if (!traderMet)
            {
                return false;
            }
        }

        if (gate.Target is not null)
        {
            var value = GetVariableValue(profile, gate.Target.Value);
            if (gate.Exact == true ? value != (gate.Value ?? 0) : value < (gate.Value ?? 0))
            {
                return false;
            }
        }

        if (
            gate.NotBuiltArea is not null
            && (profile.Hideout?.Areas ?? []).Any(area => area.Type == gate.NotBuiltArea && (area.Level ?? 0) > 0)
        )
        {
            return false;
        }

        if (gate.Level is not null && (profile.Info?.Level ?? 0) < gate.Level.Value)
        {
            return false;
        }

        var prerequisites = gate.Quests ?? [];
        if (
            !prerequisites.All(prerequisite => profile.GetQuestStatus(prerequisite) == QuestStatusEnum.Success)
            || (gate.AnyQuests is not null && !gate.AnyQuests.Any(quest => profile.GetQuestStatus(quest) == QuestStatusEnum.Success))
            || !(gate.Failed ?? []).All(quest => profile.GetQuestStatus(quest) == QuestStatusEnum.Fail)
            || !(gate.NotFailed ?? []).All(quest => profile.GetQuestStatus(quest) != QuestStatusEnum.Fail)
        )
        {
            return false;
        }

        if (gate.Delay is null || prerequisites.Count == 0)
        {
            return true;
        }

        var finishedAt = prerequisites.Max(prerequisite =>
            profile.Quests!.First(quest => quest.QId == prerequisite).StatusTimers?.GetValueOrDefault(QuestStatusEnum.Success) ?? 0
        );

        return finishedAt + gate.Delay.Value <= timeUtil.GetTimeStamp();
    }

    /// <summary>
    ///     Get a story variable from the profile. A variable group is the sum of its member variables, which trader
    ///     dialogue and quest rewards set as the story advances
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="variableId">Variable or variable group</param>
    /// <returns>Current value</returns>
    public int GetVariableValue(PmcData profile, MongoId variableId)
    {
        var variables = profile.Variables ?? [];
        var group = templateTable.VariableGroups.FirstOrDefault(group => group.Id == variableId);

        return group is null ? variables.GetValueOrDefault(variableId) : group.Variables.Sum(member => variables.GetValueOrDefault(member));
    }

    /// <summary>
    ///     Check whether a task of the last step the server can give has succeeded
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="tasks">Chapter tasks in order</param>
    /// <returns>True when the chapter's final step is done</returns>
    public bool FinalStepDone(PmcData profile, List<MongoId> tasks)
    {
        var last = tasks.FindLastIndex(IsAvailableTask);

        return last >= 0 && StepDone(profile, tasks, last);
    }

    /// <summary>
    ///     Check whether any task of the step ending at this index has succeeded
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="tasks">Chapter tasks in order</param>
    /// <param name="index">Index of the last task of the step</param>
    /// <returns>True when the step is done</returns>
    protected bool StepDone(PmcData profile, List<MongoId> tasks, int index)
    {
        for (var i = StepStart(tasks, index); i <= index; i++)
        {
            if (profile.GetQuestStatus(tasks[i]) == QuestStatusEnum.Success)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Get the first task of the step holding the task at this index. A task that fails once the task before it
    ///     succeeds is an alternative to it, and so are neighbouring tasks that quests outside the chapter start
    /// </summary>
    /// <param name="tasks">Chapter tasks in order</param>
    /// <param name="index">Index of a task in the step</param>
    /// <returns>Index of the step's first task</returns>
    protected int StepStart(List<MongoId> tasks, int index)
    {
        while (
            index > 0
            && (
                FailsOnSuccessOf(tasks[index], tasks[index - 1])
                || (StartedFromOutside(tasks[index], tasks) && StartedFromOutside(tasks[index - 1], tasks))
            )
        )
        {
            index--;
        }

        return index;
    }

    /// <summary>
    ///     Check whether a task is started by finishing quests that are none of the chapter's own tasks
    /// </summary>
    /// <param name="taskId">Task to check</param>
    /// <param name="chapterTasks">Tasks of its chapter</param>
    /// <returns>True when only outside quests start the task</returns>
    protected bool StartedFromOutside(MongoId taskId, List<MongoId> chapterTasks)
    {
        var prerequisites = (templateTable.Quests.GetValueOrDefault(taskId)?.Conditions?.AvailableForStart ?? []).GetQuestTargets();

        return prerequisites.Count > 0 && prerequisites.All(prerequisite => !chapterTasks.Contains(prerequisite));
    }

    /// <summary>
    ///     Check whether a task may only start while another runs, a side task done during it such as Falling Skies'
    ///     dollar collection during the SUV task, not an alternative to it
    /// </summary>
    /// <param name="taskId">Task to check</param>
    /// <param name="otherTaskId">Task it may depend on</param>
    /// <returns>True when the task needs the other one running</returns>
    protected bool StartsOnlyWhileRunning(MongoId taskId, MongoId otherTaskId)
    {
        return (templateTable.Quests.GetValueOrDefault(taskId)?.Conditions?.AvailableForStart ?? []).Any(condition =>
            condition.IsQuestConditionOn(otherTaskId)
            && condition.Status is not null
            && condition.Status.Count == 1
            && condition.Status.Contains(QuestStatusEnum.Started)
        );
    }

    /// <summary>
    ///     Check whether a task fails the moment another task succeeds
    /// </summary>
    /// <param name="taskId">Task to check</param>
    /// <param name="otherTaskId">Task whose success may fail it</param>
    /// <returns>True when the task is an alternative to the other one</returns>
    protected bool FailsOnSuccessOf(MongoId taskId, MongoId otherTaskId)
    {
        return (templateTable.Quests.GetValueOrDefault(taskId)?.Conditions?.Fail ?? []).Any(condition =>
            condition.IsQuestConditionOn(otherTaskId) && (condition.Status?.Contains(QuestStatusEnum.Success) ?? false)
        );
    }

    /// <summary>
    ///     Check a GlobalVariableValue condition against the profile
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="condition">Condition to check</param>
    /// <returns>True when the variable meets the condition</returns>
    protected bool VariableConditionMet(PmcData profile, QuestCondition condition)
    {
        var current = GetVariableValue(profile, condition.Target!.Item!);
        var required = condition.Value ?? 0;

        return condition.CompareMethod switch
        {
            "==" => current == required,
            ">" => current > required,
            _ => current >= required,
        };
    }

    private Dictionary<MongoId, HashSet<MongoId>> BuildChapterStarters()
    {
        var starters = new Dictionary<MongoId, HashSet<MongoId>>();
        foreach (var (questId, quest) in templateTable.Quests)
        {
            foreach (var chapterId in (quest.Conditions?.Fail ?? []).GetQuestTargets().Where(IsStoryChapter))
            {
                starters.TryAdd(chapterId, []);
                starters[chapterId].Add(questId);
            }
        }

        return starters;
    }

    private Dictionary<MongoId, MongoId> BuildTaskChapters()
    {
        Dictionary<MongoId, MongoId> taskChapters = [];
        foreach (var chapterId in templateTable.MainQuestNotes.Select(note => new MongoId(note.ChapterId)).Distinct())
        {
            foreach (var taskId in ChapterTasks(chapterId))
            {
                taskChapters.TryAdd(taskId, chapterId);
            }
        }

        return taskChapters;
    }
}
