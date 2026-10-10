using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Locales;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class EndingController(
    ISptLogger<EndingController> logger,
    TemplateTable templateTable,
    LocaleTable localeTable,
    LocaleService localeService,
    ProfileHelper profileHelper,
    QuestHelper questHelper,
    RewardHelper rewardHelper,
    MailSendService mailSendService,
    SaveServer saveServer,
    EventOutputHolder eventOutputHolder
)
{
    public async Task ObtainEnding(MongoId sessionId, EndingRequest request)
    {
        var fullProfile = profileHelper.GetFullProfile(sessionId);
        var pmcData = fullProfile?.CharacterData?.PmcData;
        var ending = FindEnding(request.EndingId);
        if (fullProfile is null || pmcData is null || ending is null)
        {
            logger.Error($"Unable to obtain ending {request.EndingId} for {sessionId}");

            return;
        }

        pmcData.Ending ??= new ProfileEnding();
        pmcData.Ending.Current = ending.Id;
        pmcData.Ending.Display = ending.Id;
        pmcData.Ending.Achieved ??= [];
        if (!pmcData.Ending.Achieved.Contains(ending.Id))
        {
            pmcData.Ending.Achieved.Add(ending.Id);
        }

        var rewards = (ending.Rewards ?? []).Where(reward => reward.Type != RewardType.Stub);
        var rewardItems = rewardHelper.ApplyRewards(rewards, CustomisationSource.UNLOCKED_IN_GAME, fullProfile, pmcData, ending.Id);
        if (rewardItems.Count > 0)
        {
            mailSendService.SendSystemMessageToPlayer(sessionId, "Ending reward", rewardItems);
        }

        CompleteEndingQuests(sessionId, pmcData, ending);

        await saveServer.SaveProfileAsync(sessionId);
    }

    /// <summary>
    ///     Hand in the finishable quests an ending is reached through. The client never hands them in itself
    ///     and their Success rewards carry the ending's money, weapons and cases
    /// </summary>
    /// <param name="sessionId">Session/player id</param>
    /// <param name="pmcData">Player's pmc profile</param>
    /// <param name="ending">Ending that was obtained</param>
    private void CompleteEndingQuests(MongoId sessionId, PmcData pmcData, EndingElement ending)
    {
        var questIds = (ending.Conditions ?? [])
            .Where(condition => condition.ConditionType == "Quest" && condition.Target?.Item is not null)
            .Select(condition => new MongoId(condition.Target!.Item!));
        foreach (var questId in questIds)
        {
            var questStatus = pmcData.Quests?.FirstOrDefault(quest => quest.QId == questId);
            if (questStatus?.Status != QuestStatusEnum.AvailableForFinish)
            {
                continue;
            }

            questHelper.CompleteQuest(
                pmcData,
                new CompleteQuestRequestData
                {
                    Action = "CompleteQuest",
                    QuestId = questId,
                    RemoveExcessItems = false,
                },
                sessionId
            );
        }
    }

    /// <summary>
    ///     Handle SetCurrentEnding, pick which reached ending the character shows
    /// </summary>
    /// <param name="pmcData">Player's pmc profile</param>
    /// <param name="request">Ending to show</param>
    /// <param name="sessionId">Session/player id</param>
    /// <returns>Item event output</returns>
    public ItemEventRouterResponse SetDisplayedEnding(PmcData pmcData, SetDisplayedEndingRequest request, MongoId sessionId)
    {
        var output = eventOutputHolder.GetOutput(sessionId);
        if (request.EndingId is null || pmcData.Ending?.Achieved?.Contains(request.EndingId) != true)
        {
            logger.Error($"Unable to show ending {request.EndingId} for {sessionId}, it was never reached");

            return output;
        }

        pmcData.Ending.Display = request.EndingId;

        return output;
    }

    public EndingLocalizationResponse GetLocalization(EndingRequest request)
    {
        var response = new EndingLocalizationResponse { Localization = [] };
        var ending = FindEnding(request.EndingId);
        if (ending is null)
        {
            return response;
        }

        var keys = new List<string>
        {
            $"{ending.SystemName} name",
            $"{ending.SystemName} caption",
            $"{ending.SystemName} description",
            $"{ending.SystemName}_name",
            $"{ending.SystemName}_caption",
            $"{ending.SystemName}_description",
            $"{ending.SystemName}.consequence",
        };
        foreach (var consequence in ending.Consequences ?? [])
        {
            keys.Add(consequence.LocalizationKeyCaptionPve ?? string.Empty);
            keys.Add(consequence.LocalizationKeyCaptionPvp ?? string.Empty);
        }

        foreach (var language in localeTable.Global.Keys)
        {
            var locale = localeService.GetLocaleDb(language);
            var texts = new Dictionary<string, string>();
            foreach (var key in keys.Distinct())
            {
                if (locale.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text))
                {
                    texts[key] = text;
                }
            }

            if (texts.Count > 0)
            {
                response.Localization[language] = texts;
            }
        }

        return response;
    }

    private EndingElement? FindEnding(MongoId endingId)
    {
        return templateTable.Endings.Elements.FirstOrDefault(element => element.Id == endingId);
    }
}
