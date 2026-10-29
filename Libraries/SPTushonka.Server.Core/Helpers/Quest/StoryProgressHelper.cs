using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SPTarkov.Server.Core.Helpers.Quest;

/// <summary>
///     Starts and completes the quests the story moves on by itself, chapters, chapter tasks and hidden steps, following
///     the rules of <see cref="MainStoryChapterHelper"/>
/// </summary>
[Injectable]
public class StoryProgressHelper(
    TemplateTable templateTable,
    TimeUtil timeUtil,
    EventOutputHolder eventOutputHolder,
    QuestRewardHelper questRewardHelper,
    QuestVariableHelper questVariableHelper,
    QuestMessageHelper questMessageHelper,
    QuestAvailabilityHelper questAvailabilityHelper,
    MainStoryChapterHelper mainStoryChapterHelper,
    QuestConfig questConfig,
    ICloner cloner
)
{
    private static readonly MongoId _guideChapterId = new("68cbd33676fe74b1e80bfd91");

    /// <summary>
    ///     Complete every running chapter that has no open task left. The client never completes a chapter itself, it
    ///     lists tasks players never get and alternatives that end failed
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="shownQuests">Quests the player is shown, with their resolved status</param>
    /// <returns>True when a chapter was completed</returns>
    public bool CompleteFinishedChapters(PmcData profile, List<Models.Eft.Common.Tables.Quest> shownQuests)
    {
        var finished = profile
            .Quests!.Where(profileQuest =>
                profileQuest.Status == QuestStatusEnum.Started && mainStoryChapterHelper.IsStoryChapter(profileQuest.QId)
            )
            .Select(profileQuest => profileQuest.QId)
            .Where(chapterId => !mainStoryChapterHelper.HasOpenTask(profile, chapterId, shownQuests))
            .ToList();

        foreach (var chapterId in finished)
        {
            CompleteChapter(profile, chapterId);
        }

        return finished.Count > 0;
    }

    /// <summary>
    ///     Mark the notes, tapes and patches among a finished quest's objectives as read
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="questId">Quest that finished</param>
    public void RecordCompletableItems(PmcData profile, MongoId questId)
    {
        if (!templateTable.Quests.TryGetValue(questId, out var quest))
        {
            return;
        }

        foreach (
            var condition in (quest.Conditions?.AvailableForFinish ?? []).Where(condition =>
                condition.ConditionType == "CompletableItem" && condition.Target?.Item is not null
            )
        )
        {
            profile.CompletableItems ??= [];
            profile.CompletableItems[condition.Target!.Item!] = true;
        }
    }

    /// <summary>
    ///     Get the status an unlocked quest is shown with. Hidden quests have no trader screen to accept them from, so
    ///     they start the moment they unlock
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="quest">Quest to resolve</param>
    /// <param name="unlockedByQuest">Whether a finished quest or an open hidden gate unlocked it</param>
    /// <returns>Status to show, or null when the quest stays hidden</returns>
    public QuestStatusEnum? ResolveStorylineStatus(PmcData profile, Models.Eft.Common.Tables.Quest quest, bool unlockedByQuest)
    {
        var status = ResolveChapterStatus(profile, quest, unlockedByQuest);
        if (status == QuestStatusEnum.AvailableForStart && quest.NotDisplayedQuest == true)
        {
            return StartStorylineQuest(profile, quest.Id);
        }

        return status;
    }

    /// <summary>
    ///     Start the chapter of a task that is active by any route, a raid, the client or an accepted dialogue
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="taskId">Task to check</param>
    /// <param name="status">Status of the task in the profile</param>
    public void StartChapterOfActiveTask(PmcData profile, MongoId taskId, QuestStatusEnum status)
    {
        if (status is QuestStatusEnum.Locked or QuestStatusEnum.AvailableForStart)
        {
            return;
        }

        var chapterId = mainStoryChapterHelper.ChapterOf(taskId);
        if (chapterId is not null && !profile.Quests!.Any(profileQuest => profileQuest.QId == chapterId))
        {
            StartStorylineQuest(profile, chapterId.Value);
        }
    }

    /// <summary>
    ///     Start a quest the story starts by itself and give its Started rewards
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="questId">Quest to start</param>
    /// <returns>Status of the quest afterwards</returns>
    public QuestStatusEnum StartStorylineQuest(PmcData profile, MongoId questId)
    {
        profile.Quests ??= [];

        var startedAt = timeUtil.GetTimeStamp();
        var existing = profile.Quests.FirstOrDefault(profileQuest => profileQuest.QId == questId);
        if (existing is not null)
        {
            if (existing.Status != QuestStatusEnum.AvailableForStart)
            {
                return existing.Status;
            }

            existing.Status = QuestStatusEnum.Started;
            existing.StartTime = startedAt;
            existing.StatusTimers ??= [];
            existing.StatusTimers[QuestStatusEnum.Started] = startedAt;
            ApplyAutoStartRewards(profile, questId);

            return QuestStatusEnum.Started;
        }

        profile.Quests.Add(
            new QuestStatus
            {
                QId = questId,
                StartTime = startedAt,
                Status = QuestStatusEnum.Started,
                StatusTimers = new Dictionary<QuestStatusEnum, double> { { QuestStatusEnum.Started, startedAt } },
            }
        );
        ApplyAutoStartRewards(profile, questId);

        return QuestStatusEnum.Started;
    }

    /// <summary>
    ///     Give the Started rewards of a quest that began without the player accepting it at a trader, the way
    ///     accepting does. Chapter start variables feed the story variable groups that gate later quests
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="questId">Quest that just started</param>
    public void ApplyAutoStartRewards(PmcData profile, MongoId questId)
    {
        if (profile.SessionId is null || !templateTable.Quests.TryGetValue(questId, out var quest))
        {
            return;
        }

        var sessionId = profile.SessionId.Value;
        var rewardItems = questRewardHelper
            .ApplyQuestReward(profile, questId, QuestStatusEnum.Started, sessionId, eventOutputHolder.GetOutput(sessionId))
            .ToList();

        if (rewardItems.Count > 0)
        {
            questMessageHelper.SendQuestStartMessage(profile, quest, rewardItems);
        }
    }

    /// <summary>
    ///     Start the quests configured to begin when the player accepts a quest
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="acceptedQuestId">Quest the player accepted</param>
    /// <returns>Templates of the quests that started</returns>
    public List<Models.Eft.Common.Tables.Quest> StartQuestsStartedWith(PmcData profile, MongoId acceptedQuestId)
    {
        List<Models.Eft.Common.Tables.Quest> started = [];
        foreach (var (questId, startedWith) in questConfig.StartedWithQuests)
        {
            if (
                startedWith != acceptedQuestId
                || !questAvailabilityHelper.ShowEventQuestToPlayer(questId)
                || !templateTable.Quests.TryGetValue(questId, out var quest)
            )
            {
                continue;
            }

            if (profile.GetQuestStatus(questId) is not (QuestStatusEnum.Locked or QuestStatusEnum.AvailableForStart))
            {
                continue;
            }

            var template = cloner.Clone(quest)!;
            template.SptStatus = StartStorylineQuest(profile, questId);
            template.Status = QuestStatusEnum.Started;
            started.Add(template);

            // Starting the quest only mails start rewards, the trader still sends the start text without them
            if (!(quest.Rewards?.GetValueOrDefault("Started")?.Count > 0))
            {
                questMessageHelper.SendQuestStartMessage(profile, quest, []);
            }
        }

        return started;
    }

    /// <summary>
    ///     Get the status of a chapter or chapter task and start what the story starts by itself. The Tour chapter
    ///     starts with the character, every other chapter starts with the first of its tasks
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="quest">Quest to resolve</param>
    /// <param name="unlockedByQuest">Whether a finished quest or an open hidden gate unlocked it</param>
    /// <returns>Status to show, or null when the quest stays hidden</returns>
    protected QuestStatusEnum? ResolveChapterStatus(PmcData profile, Models.Eft.Common.Tables.Quest quest, bool unlockedByQuest)
    {
        if (mainStoryChapterHelper.IsStoryChapter(quest.Id))
        {
            return quest.Id == _guideChapterId ? StartStorylineQuest(profile, quest.Id) : null;
        }

        var chapterId = mainStoryChapterHelper.ChapterOf(quest.Id);
        if (chapterId is null)
        {
            return QuestStatusEnum.AvailableForStart;
        }

        // A task that would fail the moment it started belongs to a branch the player did not take
        if (mainStoryChapterHelper.FailConditionsMet(profile, quest))
        {
            return null;
        }

        var chapterStarted =
            profile.Quests?.Any(profileQuest => profileQuest.QId == chapterId && profileQuest.Status == QuestStatusEnum.Started) ?? false;

        // A finished chapter starts nothing new, even when a leftover branch still qualifies
        if (profile.GetQuestStatus(chapterId.Value) is QuestStatusEnum.Success or QuestStatusEnum.Fail)
        {
            return null;
        }

        // A finished starter task opens its chapter and the chapter's first task in the same moment, every
        // later task not gated on another quest starts when the task listed before it in the chapter is done.
        // Until then the task is not listed at all
        if (!unlockedByQuest)
        {
            if (!chapterStarted && !mainStoryChapterHelper.StarterCompleted(profile, chapterId.Value))
            {
                return null;
            }

            StartStorylineQuest(profile, chapterId.Value);

            if (mainStoryChapterHelper.PredecessorCompleted(profile, chapterId.Value, quest.Id))
            {
                return StartStorylineQuest(profile, quest.Id);
            }

            // A running wait task finishes once this one starts, so the dialogue that hands it out needs it on offer
            return mainStoryChapterHelper.AcceptedInDialogue(quest.Id) && mainStoryChapterHelper.AwaitedByRunningTask(profile, quest.Id)
                ? QuestStatusEnum.AvailableForStart
                : null;
        }

        // A task the player accepts in a trader dialogue waits there, and so does its chapter
        if (mainStoryChapterHelper.AcceptedInDialogue(quest.Id))
        {
            return QuestStatusEnum.AvailableForStart;
        }

        if (!chapterStarted)
        {
            StartStorylineQuest(profile, chapterId.Value);
        }

        return StartStorylineQuest(profile, quest.Id);
    }

    /// <summary>
    ///     Complete a chapter and hand out its Success rewards the way a turn in does
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="chapterId">Chapter to complete</param>
    protected void CompleteChapter(PmcData profile, MongoId chapterId)
    {
        profile.SetQuestStatus(chapterId, QuestStatusEnum.Success, timeUtil.GetTimeStamp());
        if (profile.SessionId is null || !templateTable.Quests.TryGetValue(chapterId, out var chapter))
        {
            return;
        }

        var sessionId = profile.SessionId.Value;
        var output = eventOutputHolder.GetOutput(sessionId);
        output.ProfileChanges.TryGetValue(sessionId, out var profileChanges);
        questVariableHelper.RecordCompletion(profile, chapterId, profileChanges);
        var rewards = questRewardHelper.ApplyQuestReward(profile, chapterId, QuestStatusEnum.Success, sessionId, output).ToList();
        questMessageHelper.SendQuestSuccessMessage(sessionId, profile, chapter, rewards);
    }
}
