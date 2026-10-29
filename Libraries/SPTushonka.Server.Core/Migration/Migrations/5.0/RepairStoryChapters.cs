using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Migration.Migrations._5._0;

/// <summary>
///     Earlier 5.0 builds closed story chapters while a task was still to come and left the alternative to a chosen task
///     running. Reopen those chapters and fail those alternatives.
/// </summary>
[Injectable]
public sealed class RepairStoryChapters(MainStoryChapterHelper mainStoryChapterHelper, JsonUtil jsonUtil, TimeUtil timeUtil)
    : AbstractProfileMigration
{
    public override string MigrationName
    {
        get { return "RepairStoryChapters500"; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        if (!profile.TryGetObject(out var pmc, "characters", "pmc") || !pmc.TryGetArray(out var quests, "Quests"))
        {
            return false;
        }

        // The checks read only the quest statuses and story variables
        var questData = new PmcData
        {
            Quests = jsonUtil.Deserialize<List<QuestStatus>>(quests.ToJsonString()),
            Variables = pmc.TryGetObject(out var variables, "Variables") ? jsonUtil.Deserialize<Dictionary<MongoId, int>>(variables.ToJsonString()) : [],
        };

        return ChaptersClosedTooEarly(questData).Count > 0 || RunningAlternatives(questData).Count > 0;
    }

    // Quest logic needs the typed profile
    public override bool PostMigrate(SptProfile profile)
    {
        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Quests is not null)
        {
            Repair(pmc);
        }

        return true;
    }

    /// <summary>
    ///     Reopen chapters closed while a task was still to come, and fail chapter alternatives left running next to
    ///     the one the player finished
    /// </summary>
    /// <param name="profile">Player profile</param>
    public void Repair(PmcData profile)
    {
        foreach (var chapter in ChaptersClosedTooEarly(profile))
        {
            chapter.Status = QuestStatusEnum.Started;
            chapter.StatusTimers.Remove(QuestStatusEnum.Success);
        }

        foreach (var alternative in RunningAlternatives(profile))
        {
            profile.SetQuestStatus(alternative.Id, QuestStatusEnum.Fail, timeUtil.GetTimeStamp());
        }
    }

    /// <summary>
    ///     Get the finished chapters that still have a task coming while their final step is not done
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <returns>Profile entries of the chapters to reopen</returns>
    private List<QuestStatus> ChaptersClosedTooEarly(PmcData profile)
    {
        return (profile.Quests ?? [])
            .Where(quest => quest.Status == QuestStatusEnum.Success && mainStoryChapterHelper.IsStoryChapter(quest.QId))
            .Where(chapter => ClosedTooEarly(profile, chapter.QId))
            .ToList();
    }

    /// <summary>
    ///     Get the chapter tasks still running next to an alternative the player already finished
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <returns>Templates of the alternatives to fail</returns>
    private List<Quest> RunningAlternatives(PmcData profile)
    {
        return (profile.Quests ?? [])
            .Where(quest => quest.Status == QuestStatusEnum.Success && mainStoryChapterHelper.ChapterOf(quest.QId) is not null)
            .SelectMany(task => mainStoryChapterHelper.RunningAlternativesOf(task.QId, profile))
            .DistinctBy(alternative => alternative.Id)
            .ToList();
    }

    /// <summary>
    ///     Check whether a finished chapter still has a task coming while its final step is not done
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="chapterId">Chapter to check</param>
    /// <returns>True when the chapter was closed too early</returns>
    private bool ClosedTooEarly(PmcData profile, MongoId chapterId)
    {
        var tasks = mainStoryChapterHelper.ChapterTasks(chapterId);
        if (mainStoryChapterHelper.FinalStepDone(profile, tasks))
        {
            return false;
        }

        return tasks
            .Where(mainStoryChapterHelper.IsAvailableTask)
            .Any(taskId =>
                profile.GetQuestStatus(taskId) is not (QuestStatusEnum.Success or QuestStatusEnum.Fail)
                && (
                    mainStoryChapterHelper.TaskStillComing(profile, taskId)
                    || (mainStoryChapterHelper.AcceptedInDialogue(taskId) && mainStoryChapterHelper.AwaitedByRunningTask(profile, taskId))
                )
            );
    }
}
