using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Spt.Servers;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Callbacks;

[Injectable]
public class QuestCallbacks(
    HttpResponseUtil httpResponseUtil,
    QuestController questController,
    RepeatableQuestController repeatableQuestController,
    TemplateTable templateTable
)
{
    /// <summary>
    ///     Handle RepeatableQuestChange event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="info"></param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns></returns>
    public ValueTask<ItemEventRouterResponse> ChangeRepeatableQuest(PmcData pmcData, RepeatableQuestChangeRequest info, MongoId sessionID)
    {
        return new ValueTask<ItemEventRouterResponse>(repeatableQuestController.ChangeRepeatableQuest(pmcData, info, sessionID));
    }

    /// <summary>
    ///     Handle AddQuestNote event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="info"></param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns></returns>
    public ValueTask<ItemEventRouterResponse> AddQuestNote(PmcData pmcData, AddQuestNoteRequest info, MongoId sessionID)
    {
        return new ValueTask<ItemEventRouterResponse>(questController.AddQuestNote(pmcData, info, sessionID));
    }

    /// <summary>
    ///     Handle ReadQuestData event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="info"></param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns></returns>
    public ValueTask<ItemEventRouterResponse> ReadQuestData(PmcData pmcData, ReadQuestDataRequest info, MongoId sessionID)
    {
        return new ValueTask<ItemEventRouterResponse>(questController.ReadQuestData(pmcData, info, sessionID));
    }

    /// <summary>
    ///     Handle QuestAccept event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="info"></param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns></returns>
    public ValueTask<ItemEventRouterResponse> AcceptQuest(PmcData pmcData, AcceptQuestRequestData info, MongoId sessionID)
    {
        if (info.Type == "repeatable")
        {
            return new ValueTask<ItemEventRouterResponse>(repeatableQuestController.AcceptRepeatableQuest(pmcData, info, sessionID));
        }

        return new ValueTask<ItemEventRouterResponse>(questController.AcceptQuest(pmcData, info, sessionID));
    }

    /// <summary>
    ///     Handle QuestComplete event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="info"></param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns></returns>
    public ValueTask<ItemEventRouterResponse> CompleteQuest(PmcData pmcData, CompleteQuestRequestData info, MongoId sessionID)
    {
        return new ValueTask<ItemEventRouterResponse>(questController.CompleteQuest(pmcData, info, sessionID));
    }

    /// <summary>
    ///     Handle QuestHandover event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="info"></param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns></returns>
    public ValueTask<ItemEventRouterResponse> HandoverQuest(PmcData pmcData, HandoverQuestRequestData info, MongoId sessionID)
    {
        return new ValueTask<ItemEventRouterResponse>(questController.HandoverQuest(pmcData, info, sessionID));
    }

    /// <summary>
    ///     Handle client/quest/chains
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetQuestChains(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(templateTable.QuestChains));
    }

    /// <summary>
    ///     Handle client/quest/complete
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> CompleteQuest(string url, CompleteStoryQuestRequest info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(questController.CompleteStoryQuest(sessionID, info)));
    }

    /// <summary>
    ///     Handle client/completable-item/quests/list
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetCompletableItemQuests(string url, EmptyRequestData info, MongoId sessionID)
    {
        //Todo: Implement!
        return new ValueTask<string>(httpResponseUtil.GetBody<List<object>>([]));
    }

    /// <summary>
    ///     Handle client/quest/list
    /// </summary>
    /// <param name="url"></param>
    /// <param name="info"></param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> ListQuests(string url, ListQuestsRequestData info, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(questController.GetClientQuests(sessionID)));
    }

    /// <summary>
    ///     Handle client/repeatalbeQuests/activityPeriods
    ///     <para>
    ///     Yes the typo is intended, BSG has it in the live client as well and it has to match
    ///     </para>
    /// </summary>
    /// <param name="url"></param>
    /// <param name="_"></param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns></returns>
    public ValueTask<string> ActivityPeriods(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(repeatableQuestController.GetClientRepeatableQuests(sessionID)));
    }
}
