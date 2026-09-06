using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Commerce;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class EndingController(
    ISptLogger<EndingController> logger,
    TemplateTable templateTable,
    LocaleTable localeTable,
    LocaleService localeService,
    ProfileHelper profileHelper,
    RewardHelper rewardHelper,
    MailSendService mailSendService,
    SaveServer saveServer
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

        await saveServer.SaveProfileAsync(sessionId);
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
