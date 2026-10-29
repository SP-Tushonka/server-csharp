using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Helpers.Quest;

/// <summary>
///     Quest text lookups and the trader mails sent when a quest starts, succeeds or fails
/// </summary>
[Injectable]
public class QuestMessageHelper(LocaleService localeService, MailSendService mailSendService, TimeUtil timeUtil, QuestConfig questConfig)
{
    /// <summary>
    ///     Get quest name by quest id
    /// </summary>
    /// <param name="questId">id to get</param>
    /// <returns></returns>
    public string GetQuestNameFromLocale(string questId)
    {
        var questNameKey = $"{questId} name";
        return localeService.GetLocaleDb().GetValueOrDefault(questNameKey, "UNKNOWN");
    }

    /// <summary>
    ///     Get a quests startedMessageText key from db, if no startedMessageText key found, use description key instead
    /// </summary>
    /// <param name="quest">Quest the message belongs to</param>
    /// <returns>message id</returns>
    public string GetMessageIdForQuestStart(Models.Eft.Common.Tables.Quest quest)
    {
        // Blank or is a guid, use description instead
        var startedMessageText = GetQuestMessageText(quest, quest.StartedMessageText);
        if (
            startedMessageText.Trim() == ""
            || string.Equals(startedMessageText, "test", StringComparison.OrdinalIgnoreCase)
            || startedMessageText.Length == 24
        )
        {
            return quest.Description;
        }

        return quest.StartedMessageText!;
    }

    /// <summary>
    ///     Resolve a quest message id to the text the client will show for it
    /// </summary>
    /// <param name="quest">Quest the message belongs to</param>
    /// <param name="questMessageId">Quest message id to look up</param>
    /// <returns>Localised text, blank when the id resolves to nothing</returns>
    public string GetQuestMessageText(Models.Eft.Common.Tables.Quest quest, string? questMessageId)
    {
        if (string.IsNullOrEmpty(questMessageId))
        {
            return "";
        }

        var questLocale = GetQuestLocale(quest);
        if (questLocale is not null && questLocale.TryGetValue(questMessageId, out var questText))
        {
            return questText ?? "";
        }

        return GetQuestLocaleIdFromDb(questMessageId) ?? "";
    }

    /// <summary>
    ///     Get the locale Id from locale db for a quest message
    /// </summary>
    /// <param name="questMessageId">Quest message id to look up</param>
    /// <returns>Locale Id from locale db</returns>
    public string? GetQuestLocaleIdFromDb(string questMessageId)
    {
        var locale = localeService.GetLocaleDb();
        return locale!.GetValueOrDefault(questMessageId, null);
    }

    /// <summary>
    ///     Decide whether a quest mail is worth sending. A quest whose locale entry is blank reaches the
    ///     messenger as an empty message, which is only useful when items are attached
    /// </summary>
    /// <param name="quest">Quest the message belongs to</param>
    /// <param name="questMessageId">Quest message id the mail would carry</param>
    /// <param name="rewards">Items attached to the mail</param>
    /// <returns>True when the mail has something to show</returns>
    public bool QuestMessageIsWorthSending(Models.Eft.Common.Tables.Quest quest, string? questMessageId, ICollection<Item>? rewards)
    {
        if (rewards?.Count > 0)
        {
            return true;
        }

        return GetQuestMessageText(quest, questMessageId).Trim().Length != 0;
    }

    /// <summary>
    /// Get the hours a mails items can be collected for by profile type
    /// </summary>
    /// <param name="pmcData">Profile to get hours for</param>
    /// <returns>Hours item will be available for</returns>
    public double GetMailItemRedeemTimeHoursForProfile(PmcData pmcData)
    {
        if (!questConfig.MailRedeemTimeHours.TryGetValue(pmcData.Info!.GameVersion!, out var hours))
        {
            return questConfig.MailRedeemTimeHours["default"];
        }

        return hours;
    }

    /// <summary>
    ///     Mail the start text of a quest the player did not accept at a trader, together with its start reward items
    /// </summary>
    /// <param name="profile">Player profile</param>
    /// <param name="quest">Quest that started</param>
    /// <param name="rewardItems">Start reward items to attach</param>
    public void SendQuestStartMessage(PmcData profile, Models.Eft.Common.Tables.Quest quest, List<Item> rewardItems)
    {
        var messageId = GetMessageIdForQuestStart(quest);
        if (profile.SessionId is null || !QuestMessageIsWorthSending(quest, messageId, rewardItems))
        {
            return;
        }

        SendQuestMessage(profile.SessionId.Value, profile, quest, MessageType.QuestStart, messageId, rewardItems);
    }

    /// <summary>
    /// Send a popup to player on successful completion of a quest
    /// </summary>
    /// <param name="sessionId">session id</param>
    /// <param name="pmcData">Player profile</param>
    /// <param name="quest">Completed quest</param>
    /// <param name="questRewards">Rewards given to player</param>
    public void SendQuestSuccessMessage(MongoId sessionId, PmcData pmcData, Models.Eft.Common.Tables.Quest quest, List<Item> questRewards)
    {
        if (!QuestMessageIsWorthSending(quest, quest.SuccessMessageText, questRewards))
        {
            return;
        }

        SendQuestMessage(sessionId, pmcData, quest, MessageType.QuestSuccess, quest.SuccessMessageText ?? "", questRewards);
    }

    /// <summary>
    ///     Mail the fail text of a quest with the items its Fail rewards give
    /// </summary>
    /// <param name="sessionId">session id</param>
    /// <param name="pmcData">Player profile</param>
    /// <param name="quest">Failed quest</param>
    /// <param name="failRewards">Fail reward items to attach</param>
    public void SendQuestFailMessage(MongoId sessionId, PmcData pmcData, Models.Eft.Common.Tables.Quest quest, List<Item> failRewards)
    {
        if (!QuestMessageIsWorthSending(quest, quest.FailMessageText, failRewards))
        {
            return;
        }

        SendQuestMessage(sessionId, pmcData, quest, MessageType.QuestFail, quest.FailMessageText ?? "", failRewards);
    }

    /// <summary>
    ///     Mail a quest message from the quest's trader
    /// </summary>
    /// <param name="sessionId">session id</param>
    /// <param name="pmcData">Player profile</param>
    /// <param name="quest">Quest the message belongs to</param>
    /// <param name="messageType">Start, success or fail</param>
    /// <param name="messageId">Quest message id</param>
    /// <param name="items">Items to attach</param>
    private void SendQuestMessage(
        MongoId sessionId,
        PmcData pmcData,
        Models.Eft.Common.Tables.Quest quest,
        MessageType messageType,
        string messageId,
        List<Item> items
    )
    {
        mailSendService.SendLocalisedNpcMessageToPlayer(
            sessionId,
            quest.TraderId,
            messageType,
            messageId,
            items,
            timeUtil.GetHoursAsSeconds((int)GetMailItemRedeemTimeHoursForProfile(pmcData))
        );
    }

    /// <summary>
    ///     Get the text a quest carries for the game locale, falling back to English
    /// </summary>
    /// <param name="quest">Quest to read</param>
    /// <returns>Quest text by message id, or null when the quest carries none</returns>
    private Dictionary<string, string>? GetQuestLocale(Models.Eft.Common.Tables.Quest quest)
    {
        if (quest.Localization is null)
        {
            return null;
        }

        if (quest.Localization.TryGetValue(localeService.GetDesiredGameLocale(), out var chosenLocale))
        {
            return chosenLocale;
        }

        return quest.Localization.GetValueOrDefault("en");
    }
}
