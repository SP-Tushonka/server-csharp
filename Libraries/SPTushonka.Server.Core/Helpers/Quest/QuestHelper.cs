using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SPTarkov.Server.Core.Helpers.Quest;

[Injectable(InjectionType.Singleton)]
public class QuestHelper(
    ISptLogger<QuestHelper> logger,
    TemplateTable templateTable,
    TimeUtil timeUtil,
    EventOutputHolder eventOutputHolder,
    LocaleService localeService,
    ProfileHelper profileHelper,
    QuestRewardHelper questRewardHelper,
    QuestVariableHelper questVariableHelper,
    RewardHelper rewardHelper,
    ServerLocalisationService serverLocalisationService,
    SeasonalEventService seasonalEventService,
    MailSendService mailSendService,
    QuestConfig questConfig,
    ICloner cloner
)
{
    protected readonly FrozenSet<QuestStatusEnum> StartedOrAvailToFinish = [QuestStatusEnum.Started, QuestStatusEnum.AvailableForFinish];
    private static readonly MongoId _guideChapterId = new("68cbd33676fe74b1e80bfd91");

    private Dictionary<MongoId, MongoId>? _taskChapters;
    private Dictionary<MongoId, HashSet<MongoId>>? _chapterStarters;
    private HashSet<MongoId>? _dialogueAcceptedQuests;

    /// <summary>
    /// List of <see cref="Quest"/> conditions that require trader sales be tracked and incremented, keyed by <see cref="Quest.Id"/>
    /// We need to keep track of quests with `SellItemToTrader` finish conditions to avoid expensive lookups during trading.
    /// </summary>
    protected virtual Dictionary<MongoId, List<QuestCondition>> SellToTraderQuestConditionCache
    {
        get { return field ??= GetSellToTraderQuests(GetQuestsFromDb()); }
    }

    /// <summary>
    ///     returns true if the level condition is satisfied
    /// </summary>
    /// <param name="playerLevel">Players level</param>
    /// <param name="condition">Quest condition</param>
    /// <returns>true if player level is greater than or equal to quest</returns>
    public bool DoesPlayerLevelFulfilCondition(int playerLevel, QuestCondition condition)
    {
        if (condition.ConditionType != "Level")
        {
            return true;
        }

        var conditionValue = condition.Value;
        switch (condition.CompareMethod)
        {
            case ">=":
                return playerLevel >= conditionValue;
            case ">":
                return playerLevel > conditionValue;
            case "<":
                return playerLevel < conditionValue;
            case "<=":
                return playerLevel <= conditionValue;
            case "=":
                return conditionValue.Approx(playerLevel);
            default:
                logger.Error(serverLocalisationService.GetText("quest-unable_to_find_compare_condition", condition.CompareMethod));

                return false;
        }
    }

    /// <summary>
    ///     Get new quests in `after` that are not in `before`
    /// </summary>
    /// <param name="before">List of quests #1</param>
    /// <param name="after">List of quests #2</param>
    /// <returns>quests not in before</returns>
    public IEnumerable<Models.Eft.Common.Tables.Quest> GetDeltaQuests(
        IEnumerable<Models.Eft.Common.Tables.Quest> before,
        IEnumerable<Models.Eft.Common.Tables.Quest> after
    )
    {
        // Nothing to compare against, return after
        if (!before.Any())
        {
            return after;
        }

        // Get quests from before as a hashset for fast lookups
        var beforeQuests = before.Select(quest => quest.Id).ToHashSet();

        // Return quests found in after but not before
        return after.Where(quest => !beforeQuests.Contains(quest.Id));
    }

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
    ///     Check if trader has sufficient loyalty to fulfill quest requirement
    /// </summary>
    /// <param name="questProperties">Quest props</param>
    /// <param name="profile">Player profile</param>
    /// <returns>true if loyalty is high enough to fulfill quest requirement</returns>
    public bool TraderLoyaltyLevelRequirementCheck(QuestCondition questProperties, PmcData profile)
    {
        if (
            !profile.TradersInfo!.TryGetValue(
                questProperties.Target!.IsItem ? questProperties.Target.Item! : questProperties.Target.List!.FirstOrDefault()!,
                out var trader
            )
        )
        {
            logger.Error(serverLocalisationService.GetText("quest-unable_to_find_trader_in_profile", questProperties.Target));
        }

        return CompareAvailableForValues(trader!.LoyaltyLevel!.Value, questProperties.Value!.Value, questProperties.CompareMethod!);
    }

    /// <summary>
    ///     Check if trader has sufficient standing to fulfill quest requirement
    /// </summary>
    /// <param name="questProperties">Quest props</param>
    /// <param name="profile">Player profile</param>
    /// <returns>true if standing is high enough to fulfill quest requirement</returns>
    public bool TraderStandingRequirementCheck(QuestCondition questProperties, PmcData profile)
    {
        var requiredLoyaltyLevel = Convert.ToInt32(questProperties.Value);
        if (
            !profile.TradersInfo!.TryGetValue(
                questProperties.Target!.IsItem ? questProperties.Target.Item! : questProperties.Target.List!.FirstOrDefault()!,
                out var trader
            )
        )
        {
            logger.Error(serverLocalisationService.GetText("quest-unable_to_find_trader_in_profile", questProperties.Target));
        }

        return CompareAvailableForValues(trader?.Standing ?? 1, requiredLoyaltyLevel, questProperties.CompareMethod!);
    }

    /// <summary>
    /// Helper to map symbols to actions
    /// </summary>
    /// <param name="current">First value</param>
    /// <param name="required">Second value</param>
    /// <param name="compareMethod">Symbol to compare two values with e.g. ">="</param>
    /// <returns>Outcome of comparison</returns>
    protected bool CompareAvailableForValues(double current, double required, string compareMethod)
    {
        switch (compareMethod)
        {
            case ">=":
                return current >= required;
            case ">":
                return current > required;
            case "<=":
                return current <= required;
            case "<":
                return current < required;
            case "!=":
                return !current.Approx(required);
            case "==":
                return current.Approx(required);

            default:
                logger.Error(serverLocalisationService.GetText("quest-compare_operator_unhandled", compareMethod));

                return false;
        }
    }

    /// <summary>
    /// Look up quest in db by accepted quest id and construct a profile-ready object ready to store in profile
    /// </summary>
    /// <param name="pmcData">Player profile</param>
    /// <param name="newState">State the new quest should be in when returned</param>
    /// <param name="acceptedQuest">Details of accepted quest from client</param>
    /// <returns>quest status object for storage in profile</returns>
    public QuestStatus GetQuestReadyForProfile(PmcData pmcData, QuestStatusEnum newState, AcceptQuestRequestData acceptedQuest)
    {
        var currentTimestamp = timeUtil.GetTimeStamp();
        var existingQuest = pmcData.Quests?.FirstOrDefault(q => q.QId == acceptedQuest.QuestId);
        if (existingQuest is not null)
        {
            // Quest exists, update what's there
            existingQuest.StartTime = currentTimestamp;
            existingQuest.Status = newState;
            existingQuest.StatusTimers[newState] = currentTimestamp;
            existingQuest.CompletedConditions = [];

            if (existingQuest.AvailableAfter is not null)
            {
                existingQuest.AvailableAfter = null;
            }

            return existingQuest;
        }

        // Quest doesn't exist, add it
        var newQuest = new QuestStatus
        {
            QId = acceptedQuest.QuestId,
            StartTime = currentTimestamp,
            Status = newState,
            StatusTimers = new Dictionary<QuestStatusEnum, double>(),
        };

        // Check if quest has a prereq to be placed in a 'pending' state, otherwise set status timers value
        var questDbData = GetQuestFromDb(acceptedQuest.QuestId, pmcData);
        if (questDbData is null)
        {
            logger.Error(
                serverLocalisationService.GetText(
                    "quest-unable_to_find_quest_in_db",
                    new { questId = acceptedQuest.QuestId, questType = acceptedQuest.Type }
                )
            );
        }

        var waitTime = questDbData?.Conditions.AvailableForStart?.FirstOrDefault(x => x.AvailableAfter > 0);
        if (waitTime is not null && acceptedQuest.Type != "repeatable")
        {
            // Quest should be put into 'pending' state
            newQuest.StartTime = 0;
            newQuest.Status = QuestStatusEnum.AvailableAfter; // 9
            newQuest.AvailableAfter = currentTimestamp + waitTime.AvailableAfter;
        }
        else
        {
            newQuest.StatusTimers[newState] = currentTimestamp;
            newQuest.CompletedConditions = [];
        }

        return newQuest;
    }

    /// <summary>
    /// Get quests that can be shown to player after starting a quest
    /// </summary>
    /// <param name="startedQuestId">Quest started by player</param>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns>Quests accessible to player including newly unlocked quests now quest (startedQuestId) was started</returns>
    public List<Models.Eft.Common.Tables.Quest> GetNewlyAccessibleQuestsWhenStartingQuest(MongoId startedQuestId, MongoId sessionId)
    {
        // Get quest acceptance data from profile
        var profile = profileHelper.GetPmcProfile(sessionId);
        var startedQuestInProfile = profile?.Quests?.FirstOrDefault(profileQuest => profileQuest.QId == startedQuestId);

        // Get quests that
        var eligibleQuests = GetQuestsFromDb()
            .Where(quest =>
            {
                // Quest is accessible to player when the accepted quest passed into param is started
                // e.g. Quest A passed in, quest B is looped over and has requirement of A to be started, include it
                var matchingQuestCondition = quest.Conditions.AvailableForStart?.FirstOrDefault(condition =>
                    condition.ConditionType == "Quest"
                    && (
                        (condition.Target?.Item?.Contains(startedQuestId) ?? false)
                        || (condition.Target?.List?.Contains(startedQuestId) ?? false)
                    )
                    && (condition.Status?.Contains(QuestStatusEnum.Started) ?? false)
                );

                // Has a matching quest condition in another quest (Accepting this quest gives access to found quest too) check if it also has a level requirement that passes
                if (matchingQuestCondition is not null)
                {
                    var matchingLevelRequirement = quest.Conditions.AvailableForStart?.FirstOrDefault(condition =>
                        condition.ConditionType == "Level"
                    );
                    if (matchingLevelRequirement is not null && profile?.Info?.Level < matchingLevelRequirement.Value)
                    {
                        // Player doesn't fulfil level requirement for quest, don't show it to player
                        return false;
                    }
                }

                // Not found, skip quest
                if (matchingQuestCondition is null)
                {
                    return false;
                }

                // Skip locked event quests
                if (!ShowEventQuestToPlayer(quest.Id))
                {
                    return false;
                }

                // Skip quest if it's flagged as for other side
                if (QuestIsForOtherSide(profile?.Info?.Side, quest.Id))
                {
                    return false;
                }

                if (QuestIsProfileBlacklisted(profile?.Info?.GameVersion, quest.Id))
                {
                    return false;
                }

                if (questConfig.WithheldQuests.Contains(quest.Id))
                {
                    return false;
                }

                if (!QuestIsProfileWhitelisted(profile?.Info?.GameVersion, quest.Id))
                {
                    return false;
                }

                var standingRequirements = quest.Conditions.AvailableForStart?.GetStandingConditions();
                foreach (var condition in standingRequirements ?? [])
                {
                    if (!TraderStandingRequirementCheck(condition, profile!))
                    {
                        return false;
                    }
                }

                var loyaltyRequirements = quest.Conditions.AvailableForStart!.GetLoyaltyConditions();
                foreach (var condition in loyaltyRequirements)
                {
                    if (!TraderLoyaltyLevelRequirementCheck(condition, profile!))
                    {
                        return false;
                    }
                }

                // Include if quest found in profile and is started or ready to hand in
                return startedQuestInProfile is not null && StartedOrAvailToFinish.Contains(startedQuestInProfile.Status);
            });

        return GetQuestsWithOnlyLevelRequirementStartCondition(eligibleQuests).ToList();
    }

    /// <summary>
    /// Should a seasonal/event quest be shown to the player
    /// </summary>
    /// <param name="questId">Quest to check</param>
    /// <returns>true = show to player</returns>
    public bool ShowEventQuestToPlayer(MongoId questId)
    {
        var isChristmasEventActive = seasonalEventService.ChristmasEventEnabled();
        var isHalloweenEventActive = seasonalEventService.HalloweenEventEnabled();

        // Not christmas + quest is for christmas
        if (!isChristmasEventActive && seasonalEventService.IsQuestRelatedToEvent(questId, SeasonalEventType.Christmas))
        {
            return false;
        }

        // Not halloween + quest is for halloween
        if (!isHalloweenEventActive && seasonalEventService.IsQuestRelatedToEvent(questId, SeasonalEventType.Halloween))
        {
            return false;
        }

        // Should non-season event quests be shown to player
        if (!questConfig.ShowNonSeasonalEventQuests && seasonalEventService.IsQuestRelatedToEvent(questId, SeasonalEventType.None))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Is the quest for the opposite side the player is on
    /// </summary>
    /// <param name="playerSide">Player side (usec/bear)</param>
    /// <param name="questId">QuestId to check</param>
    /// <returns>true = quest isn't for player</returns>
    public bool QuestIsForOtherSide(string? playerSide, MongoId questId)
    {
        var isUsec = string.Equals(playerSide, "usec", StringComparison.OrdinalIgnoreCase);
        if (isUsec && questConfig.BearOnlyQuests.Contains(questId))
        // Player is usec and quest is bear only, skip
        {
            return true;
        }

        if (!isUsec && questConfig.UsecOnlyQuests.Contains(questId))
        // Player is bear and quest is usec only, skip
        {
            return true;
        }

        // player is bear + quest is usec OR player is usec + quest is bear
        return false;
    }

    /// <summary>
    /// Is the provided quest prevented from being viewed by the provided game version
    /// (Inclusive filter)
    /// </summary>
    /// <param name="gameVersion">Game version to check against</param>
    /// <param name="questId">Quest id to check</param>
    /// <returns>True = Quest should not be visible to game version</returns>
    public bool QuestIsProfileBlacklisted(string? gameVersion, MongoId questId)
    {
        if (!questConfig.ProfileBlacklist.TryGetValue(gameVersion ?? string.Empty, out var blacklistedQuests))
        {
            // Game version has no blacklist
            return false;
        }

        return blacklistedQuests.Contains(questId);
    }

    /// <summary>
    /// Is the provided quest able to be seen by the provided game version
    /// (Exclusive filter)
    /// </summary>
    /// <param name="gameVersion">Game version to check against</param>
    /// <param name="questId">Quest id to check</param>
    /// <returns>True = Quest should be visible to game version</returns>
    public bool QuestIsProfileWhitelisted(string? gameVersion, MongoId questId)
    {
        if (!questConfig.ProfileWhitelist.TryGetValue(questId, out var allowedGameVersions))
        {
            // Quest isn't whitelist restricted, visible to all game versions
            return true;
        }

        return allowedGameVersions.Contains(gameVersion ?? string.Empty);
    }

    /// <summary>
    /// Get quests that can be shown to player after failing a quest
    /// </summary>
    /// <param name="failedQuestId">Id of the quest failed by player</param>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns>List of Quest</returns>
    public List<Models.Eft.Common.Tables.Quest> FailedUnlocked(MongoId failedQuestId, MongoId sessionId)
    {
        var profile = profileHelper.GetPmcProfile(sessionId);
        var profileQuest = profile!.Quests!.FirstOrDefault(x => x.QId == failedQuestId);

        var quests = GetQuestsFromDb()
            .Where(q =>
            {
                var acceptedQuestCondition = q.Conditions.AvailableForStart!.FirstOrDefault(c =>
                    c.ConditionType == "Quest"
                    && (c.Target!.IsList ? c.Target.List : [c.Target.Item!])!.Contains(failedQuestId)
                    && c.Status!.First() == QuestStatusEnum.Fail
                );

                if (acceptedQuestCondition is null)
                {
                    return false;
                }

                return profileQuest is not null && profileQuest.Status == QuestStatusEnum.Fail;
            })
            .ToList();

        if (quests.Count != 0)
        {
            return quests;
        }

        return GetQuestsWithOnlyLevelRequirementStartCondition(quests).ToList();
    }

    /// <summary>
    /// Sets the item stack to new value, or delete the item if value is less than or equal 0
    /// </summary>
    /// <param name="pmcData">Profile</param>
    /// <param name="itemId">Id of item to adjust stack size of</param>
    /// <param name="newStackSize">Stack size to adjust to</param>
    /// <param name="sessionId">Session id</param>
    /// <param name="output">ItemEvent router response</param>
    public void ChangeItemStack(PmcData pmcData, MongoId itemId, int newStackSize, MongoId sessionId, ItemEventRouterResponse output)
    {
        //TODO: maybe merge this function and the one from customization
        var inventoryItemIndex = pmcData.Inventory!.Items!.FindIndex(item => item.Id == itemId);
        if (inventoryItemIndex < 0)
        {
            logger.Error(serverLocalisationService.GetText("quest-item_not_found_in_inventory", itemId));

            return;
        }

        if (newStackSize > 0)
        {
            var item = pmcData.Inventory.Items[inventoryItemIndex];
            item.AddUpd();

            item.Upd!.StackObjectsCount = newStackSize;

            output.AddItemStackSizeChangeIntoEventResponse(sessionId, item);
        }
        else
        {
            // this case is probably dead Code right now, since the only calling function
            // checks explicitly for Value > 0.
            output.ProfileChanges[sessionId].Items!.DeletedItems.Add(new DeletedItem { Id = itemId });
            pmcData.Inventory.Items.RemoveAt(inventoryItemIndex);
        }
    }

    /// <summary>
    /// Get quests, strip all requirement conditions except level
    /// </summary>
    /// <param name="quests">quests to process</param>
    /// <returns>quest list without conditions</returns>
    protected IEnumerable<Models.Eft.Common.Tables.Quest> GetQuestsWithOnlyLevelRequirementStartCondition(
        IEnumerable<Models.Eft.Common.Tables.Quest> quests
    )
    {
        return quests.Select(RemoveQuestConditionsExceptLevel);
    }

    /// <summary>
    /// Remove all quest conditions except for level requirement
    /// </summary>
    /// <param name="quest">quest to clean</param>
    /// <returns>Quest</returns>
    public Models.Eft.Common.Tables.Quest RemoveQuestConditionsExceptLevel(Models.Eft.Common.Tables.Quest quest)
    {
        var updatedQuest = cloner.Clone(quest);
        updatedQuest!.Conditions.AvailableForStart = updatedQuest
            .Conditions.AvailableForStart!.Where(q => q.ConditionType == "Level")
            .ToList();

        return updatedQuest;
    }

    /// <summary>
    /// Get all quests with finish condition `SellItemToTrader`.
    /// The first time this method is called it will cache the conditions by quest id in <see cref="SellToTraderQuestConditionCache"/>` and return that thereafter.
    /// </summary>
    /// <param name="quests">Quests to process</param>
    /// <returns>List of quests with `SellItemToTrader` finish condition(s)</returns>
    protected Dictionary<MongoId, List<QuestCondition>> GetSellToTraderQuests(IEnumerable<Models.Eft.Common.Tables.Quest> quests)
    {
        // Create cache
        var result = new Dictionary<MongoId, List<QuestCondition>>();
        foreach (var quest in quests)
        {
            foreach (var cond in quest.Conditions.AvailableForFinish!)
            {
                if (cond.ConditionType != "SellItemToTrader")
                {
                    continue;
                }

                if (!result.TryGetValue(quest.Id, out var questConditions))
                {
                    questConditions ??= [];
                    questConditions.Add(cond);

                    result.Add(quest.Id, questConditions);
                    continue;
                }

                questConditions.Add(cond);
            }
        }

        if (logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug($"GetSellToTraderQuests found: {result.Count} quests");
        }

        return result;
    }

    /// <summary>
    /// Get all active condition counters for `SellItemToTrader` conditions
    /// </summary>
    /// <param name="pmcData">Profile to check</param>
    /// <returns>List of active TaskConditionCounters</returns>
    protected List<TaskConditionCounter>? GetActiveSellToTraderConditionCounters(PmcData pmcData)
    {
        return pmcData
            .TaskConditionCounters?.Values.Where(condition =>
                SellToTraderQuestConditionCache.ContainsKey(condition.SourceId!.Value) && condition.Type == "SellItemToTrader"
            )
            .ToList();
    }

    /// <summary>
    /// Look over all active conditions and increment them as needed
    /// </summary>
    /// <param name="profileWithItemsToSell">profile selling the items</param>
    /// <param name="profileToReceiveMoney">profile to receive the money</param>
    /// <param name="sellRequest">request with items to sell</param>
    public void IncrementSoldToTraderCounters(
        PmcData profileWithItemsToSell,
        PmcData profileToReceiveMoney,
        ProcessSellTradeRequestData sellRequest
    )
    {
        var activeConditionCounters = GetActiveSellToTraderConditionCounters(profileToReceiveMoney);

        // No active conditions, exit
        if (activeConditionCounters is null || activeConditionCounters.Count == 0)
        {
            return;
        }

        foreach (var counter in activeConditionCounters)
        {
            // Condition is in profile, but quest doesn't exist in database
            if (!SellToTraderQuestConditionCache.TryGetValue(counter.SourceId!.Value, out var conditions))
            {
                logger.Error(serverLocalisationService.GetText("quest_unable_to_find_quest_in_db_no_type", counter.SourceId));
                continue;
            }

            foreach (var condition in conditions)
            {
                IncrementSoldToTraderCounter(profileWithItemsToSell, counter, condition, sellRequest);
            }
        }
    }

    /// <summary>
    /// Increment an individual condition counter
    /// </summary>
    /// <param name="profileWithItemsToSell">Profile selling the items</param>
    /// <param name="taskCounter">condition counter to increment</param>
    /// <param name="questCondition">quest condtion to check for valid items on</param>
    /// <param name="sellRequest">sell request of items sold</param>
    protected void IncrementSoldToTraderCounter(
        PmcData profileWithItemsToSell,
        TaskConditionCounter taskCounter,
        QuestCondition questCondition,
        ProcessSellTradeRequestData sellRequest
    )
    {
        var itemsTplsThatIncrement = questCondition.Target;
        foreach (var itemSoldToTrader in sellRequest.Items!)
        {
            // Get sold items' details from profile
            var itemDetails = profileWithItemsToSell.Inventory?.Items?.FirstOrDefault(inventoryItem =>
                inventoryItem.Id == itemSoldToTrader.Id
            );
            if (itemDetails is null)
            {
                logger.Error(
                    serverLocalisationService.GetText("trader-unable_to_find_inventory_item_for_selltotrader_counter", taskCounter.SourceId)
                );

                continue;
            }

            // Is sold item on the increment list
            if (itemsTplsThatIncrement!.List!.Contains(itemDetails.Template))
            {
                taskCounter.Value += itemSoldToTrader.Count;
            }
        }
    }

    /// <summary>
    /// Fail a quest in a player profile
    /// </summary>
    /// <param name="pmcData">Player profile</param>
    /// <param name="failRequest">Fail quest request data</param>
    /// <param name="sessionId">Player/Session id</param>
    /// <param name="output">Client output</param>
    public void FailQuest(PmcData pmcData, FailQuestRequestData failRequest, MongoId sessionId, ItemEventRouterResponse? output = null)
    {
        // Prepare response to send back to client
        var updatedOutput = output ?? eventOutputHolder.GetOutput(sessionId);

        // The client also fails quests it never accepted once their fail conditions hold
        AddQuestsToProfile(pmcData, [failRequest.QuestId], [QuestStatusEnum.Fail]);
        UpdateQuestState(pmcData, QuestStatusEnum.Fail, failRequest.QuestId);
        var questRewards = questRewardHelper.ApplyQuestReward(pmcData, failRequest.QuestId, QuestStatusEnum.Fail, sessionId, updatedOutput);

        // Create a dialog message for completing the quest.
        var quest = GetQuestFromDb(failRequest.QuestId, pmcData);

        // Merge all daily/weekly/scav daily quests into one array and look for the matching quest by id
        var matchingRepeatableQuest = pmcData
            .RepeatableQuests!.SelectMany(repeatableType => repeatableType.ActiveQuests!)
            .FirstOrDefault(activeQuest => activeQuest.Id == failRequest.QuestId);

        // Quest found and no repeatable found
        if (quest is not null && matchingRepeatableQuest is null)
        {
            var failRewards = questRewards.ToList();
            if (QuestMessageIsWorthSending(quest, quest.FailMessageText, failRewards))
            {
                mailSendService.SendLocalisedNpcMessageToPlayer(
                    sessionId,
                    quest.TraderId,
                    MessageType.QuestFail,
                    quest.FailMessageText ?? "",
                    failRewards,
                    timeUtil.GetHoursAsSeconds((int)GetMailItemRedeemTimeHoursForProfile(pmcData))
                );
            }
        }

        updatedOutput.ProfileChanges[sessionId].Quests!.AddRange(FailedUnlocked(failRequest.QuestId, sessionId));
    }

    /// <summary>
    /// Get collection of All Quests from db
    /// </summary>
    /// <remarks>NOT CLONED</remarks>
    /// <returns>List of Quest objects</returns>
    public List<Models.Eft.Common.Tables.Quest> GetQuestsFromDb()
    {
        return templateTable.Quests.Values.ToList();
    }

    /// <summary>
    /// Get quest by id from database (repeatables are stored in profile, check there if questId not found)
    /// </summary>
    /// <param name="questId">Id of quest to find</param>
    /// <param name="pmcData">Player profile</param>
    /// <returns>Found quest</returns>
    public Models.Eft.Common.Tables.Quest? GetQuestFromDb(MongoId questId, PmcData pmcData)
    {
        // Maybe a repeatable quest?
        if (templateTable.Quests.TryGetValue(questId, out var quest))
        {
            return quest;
        }

        // Check daily/weekly objects
        if (pmcData.RepeatableQuests is not null)
        {
            return pmcData.RepeatableQuests.SelectMany(x => x.ActiveQuests ?? []).FirstOrDefault(x => x.Id == questId);
        }

        return null;
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

    /// <summary>
    ///     Alter a quests state + Add a record to its status timers object
    /// </summary>
    /// <param name="pmcData">Profile to update</param>
    /// <param name="newQuestState">New state the quest should be in</param>
    /// <param name="questId">Id of the quest to alter the status of</param>
    protected void UpdateQuestState(PmcData pmcData, QuestStatusEnum newQuestState, MongoId questId)
    {
        // Find quest in profile, update status to desired status
        var questToUpdate = pmcData.Quests?.FirstOrDefault(quest => quest.QId == questId);
        if (questToUpdate is not null)
        {
            questToUpdate.Status = newQuestState;
            questToUpdate.StatusTimers[newQuestState] = timeUtil.GetTimeStamp();
        }
    }

    /// <summary>
    ///     Resets a quests values back to its chosen state
    /// </summary>
    /// <param name="pmcData">Profile to update</param>
    /// <param name="newQuestState">New state the quest should be in</param>
    /// <param name="questId">Id of the quest to alter the status of</param>
    public void ResetQuestState(PmcData pmcData, QuestStatusEnum newQuestState, MongoId questId)
    {
        var questToUpdate = pmcData.Quests?.FirstOrDefault(quest => quest.QId == questId);
        if (questToUpdate is not null)
        {
            var currentTimestamp = timeUtil.GetTimeStamp();

            questToUpdate.Status = newQuestState;

            // Only set start time when quest is being started
            if (newQuestState == QuestStatusEnum.Started)
            {
                questToUpdate.StartTime = currentTimestamp;
            }

            questToUpdate.StatusTimers[newQuestState] = currentTimestamp;

            // Delete all status timers after applying new status
            foreach (var statusKey in questToUpdate.StatusTimers)
            {
                if (statusKey.Key > newQuestState)
                {
                    questToUpdate.StatusTimers.Remove(statusKey.Key);
                }
            }

            // Remove all completed conditions
            questToUpdate.CompletedConditions = [];
        }
    }

    /// <summary>
    /// Find quest with 'findItem' condition that needs the item tpl be handed in
    /// </summary>
    /// <param name="itemTpl">item tpl to look for</param>
    /// <param name="questIds">Quests to search through for the findItem condition</param>
    /// <param name="allQuests">All quests to check</param>
    /// <returns>quest id with 'FindItem' condition id</returns>
    public Dictionary<string, string> GetFindItemConditionByQuestItem(
        MongoId itemTpl,
        MongoId[] questIds,
        List<Models.Eft.Common.Tables.Quest> allQuests
    )
    {
        Dictionary<string, string> result = new();
        foreach (var questId in questIds)
        {
            var questInDb = allQuests.FirstOrDefault(x => x.Id == questId);
            if (questInDb is null)
            {
                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug($"Unable to find quest: {questId} in db, cannot get 'FindItem' condition, skipping");
                }

                continue;
            }

            var condition = questInDb.Conditions.AvailableForFinish?.FirstOrDefault(c =>
                c.ConditionType == "FindItem" && ((c.Target!.IsList ? c.Target.List : [c.Target.Item!])?.Contains(itemTpl) ?? false)
            );
            if (condition is not null)
            {
                result[questId] = condition.Id;

                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Add all quests to a profile with the provided statuses
    /// </summary>
    /// <param name="pmcProfile">profile to update</param>
    /// <param name="statuses">statuses quests should have added to profile</param>
    public void AddAllQuestsToProfile(PmcData pmcProfile, IEnumerable<QuestStatusEnum> statuses)
    {
        AddQuestsToProfile(pmcProfile, templateTable.Quests.Keys, statuses);
    }

    /// <summary>
    /// Add the given quests to a profile with the provided statuses, quests already present are left alone
    /// </summary>
    public void AddQuestsToProfile(PmcData pmcProfile, IEnumerable<MongoId> questIds, IEnumerable<QuestStatusEnum> statuses)
    {
        foreach (var key in questIds)
        {
            if (pmcProfile.Quests!.Any(x => x.QId == key))
            {
                continue;
            }

            // Create dict of status to add to quest in profile
            var statusesDict = new Dictionary<QuestStatusEnum, double>();
            foreach (var status in statuses)
            {
                statusesDict.Add(status, timeUtil.GetTimeStamp());
            }

            var questRecordToAdd = new QuestStatus
            {
                QId = key,
                StartTime = timeUtil.GetTimeStamp(),
                Status = statuses.Last(), // Get last status in list as currently active status
                StatusTimers = statusesDict,
                CompletedConditions = [],
                AvailableAfter = 0,
            };

            // Check if the quest already exists in the profile
            var existingQuest = pmcProfile.Quests!.FirstOrDefault(x => x.QId == key);
            if (existingQuest != null)
            {
                // Update existing quest
                existingQuest.Status = questRecordToAdd.Status;
                existingQuest.StatusTimers = questRecordToAdd.StatusTimers;
            }
            else
            {
                // Add new quest to the profile
                pmcProfile.Quests!.Add(questRecordToAdd);
            }
        }
    }

    /// <summary>
    /// Find and remove the provided quest id from the provided collection of quests
    /// </summary>
    /// <param name="questId">Id of quest to remove</param>
    /// <param name="quests">Collection of quests to remove id from</param>
    public void FindAndRemoveQuestFromArrayIfExists(MongoId questId, List<QuestStatus> quests)
    {
        quests.RemoveAll(quest => quest.QId == questId);
    }

    /// <summary>
    /// Return a list of quests that would fail when supplied quest is completed
    /// </summary>
    /// <param name="completedQuestId">quest completed id</param>
    /// <returns>Collection of Quest objects</returns>
    public List<Models.Eft.Common.Tables.Quest> GetQuestsFailedByCompletingQuest(MongoId completedQuestId)
    {
        var questsInDb = GetQuestsFromDb();
        return questsInDb
            .Where(quest =>
            {
                // No fail conditions, exit early
                if (quest.Conditions.Fail is null || quest.Conditions.Fail.Count == 0)
                {
                    return false;
                }

                return quest.Conditions.Fail.Any(condition =>
                    (condition.Target!.IsList ? condition.Target.List : [condition.Target!.Item!])?.Contains(completedQuestId) ?? false
                );
            })
            .ToList();
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
    /// Handle player completing a quest
    /// Flag quest as complete in their profile
    /// Look for and flag any quests that fail when completing quest
    /// Show completed dialog on screen
    /// Add time locked quests unlocked by completing quest
    /// handle specific actions needed when quest is a repeatable
    /// </summary>
    /// <param name="pmcData">Player profile</param>
    /// <param name="request">Client request</param>
    /// <param name="sessionId">Player/session id</param>
    /// <returns>Client response</returns>
    public ItemEventRouterResponse CompleteQuest(PmcData pmcData, CompleteQuestRequestData request, MongoId sessionId)
    {
        var completeQuestResponse = eventOutputHolder.GetOutput(sessionId);
        if (!completeQuestResponse.ProfileChanges.TryGetValue(sessionId, out var profileChanges))
        {
            logger.Error($"Unable to get profile changes for {sessionId}");

            return completeQuestResponse;
        }

        // Clone of players quest status prior to any changes
        var preCompleteProfileQuestsClone = cloner.Clone(pmcData.Quests);

        // Id of quest player just completed
        var completedQuestId = request.QuestId;

        // Keep a copy of player quest statuses from their profile (Must be gathered prior to applyQuestReward() & failQuests())
        var clientQuestsClone = cloner.Clone(GetClientQuests(sessionId));

        const QuestStatusEnum newQuestState = QuestStatusEnum.Success;
        UpdateQuestState(pmcData, newQuestState, completedQuestId);
        RecordCompletableItems(pmcData, completedQuestId);
        questVariableHelper.RecordCompletion(pmcData, completedQuestId, profileChanges);
        var questRewards = questRewardHelper.ApplyQuestReward(pmcData, request.QuestId, newQuestState, sessionId, completeQuestResponse);

        // Check for linked failed + unrestartable quests (only get quests not already failed
        var questsToFail = GetQuestsFromProfileFailedByCompletingQuest(completedQuestId, pmcData);
        if (questsToFail.Count > 0)
        {
            FailQuests(sessionId, pmcData, questsToFail, completeQuestResponse);
        }

        // Show success modal on player screen
        SendSuccessDialogMessageOnQuestComplete(sessionId, pmcData, completedQuestId, questRewards.ToList());

        // Add diff of quests before completion vs after for client response
        var questDelta = GetDeltaQuests(clientQuestsClone!, GetClientQuests(sessionId));

        // Check newly available + failed quests for timegates and add them to profile
        AddTimeLockedQuestsToProfile(pmcData, questDelta, request.QuestId);

        // Inform client of quest changes
        profileChanges.Quests!.AddRange(questDelta);

        // If a repeatable quest. Remove from scav profile quests array
        foreach (var currentRepeatable in pmcData.RepeatableQuests!)
        {
            var repeatableQuest = currentRepeatable.ActiveQuests?.FirstOrDefault(activeRepeatable =>
                activeRepeatable.Id == completedQuestId
            );
            if (repeatableQuest is not null)
            // Need to remove redundant scav quest object as its no longer necessary, is tracked in pmc profile
            {
                if (repeatableQuest.Side == "Scav")
                {
                    RemoveQuestFromScavProfile(sessionId, repeatableQuest.Id);
                }
            }
        }

        // Hydrate client response questsStatus array with data
        var questStatusChanges = GetQuestsWithDifferentStatuses(preCompleteProfileQuestsClone!, pmcData.Quests!);
        profileChanges.QuestsStatus!.AddRange(questStatusChanges);

        return completeQuestResponse;
    }

    /// <summary>
    /// Handle client/quest/list
    /// Get all quests visible to player
    /// Exclude quests with incomplete preconditions (level/loyalty)
    /// </summary>
    /// <param name="sessionId">session/player id</param>
    /// <returns>Collection of quests</returns>
    public List<Models.Eft.Common.Tables.Quest> GetClientQuests(MongoId sessionId)
    {
        List<Models.Eft.Common.Tables.Quest> questsToShowPlayer = [];
        var profile = profileHelper.GetPmcProfile(sessionId);
        if (profile is null)
        {
            logger.Error($"Profile {sessionId} not found, unable to return quests");

            return [];
        }

        // A freshly registered account lists its profiles before it has a character
        if (profile.Quests is null)
        {
            return [];
        }

        var allQuests = GetQuestsFromDb();
        foreach (var quest in allQuests)
        {
            // Player already accepted the quest, show it regardless of status
            // A Locked entry is the client's own bookkeeping written back after a raid, not an accepted quest
            var questInProfile = profile.Quests!.FirstOrDefault(x => x.QId == quest.Id && x.Status != QuestStatusEnum.Locked);
            if (questInProfile is not null)
            {
                // The client writes hidden quests back as AvailableForStart, a state they can never leave by hand
                quest.SptStatus =
                    questInProfile.Status == QuestStatusEnum.AvailableForStart && quest.NotDisplayedQuest == true
                        ? StartStorylineQuest(profile, quest.Id)
                        : questInProfile.Status;
                StartChapterOfActiveTask(profile, quest.Id, quest.SptStatus.Value);
                questsToShowPlayer.Add(quest);
                continue;
            }

            // Filter out bear quests for USEC and vice versa
            if (QuestIsForOtherSide(profile.Info!.Side, quest.Id))
            {
                continue;
            }

            // Quest is blacklisted for this game version
            if (QuestIsProfileBlacklisted(profile.Info.GameVersion, quest.Id))
            {
                continue;
            }

            if (questConfig.WithheldQuests.Contains(quest.Id))
            {
                continue;
            }

            // Quest is whitelisted to game versions the profile isn't part of
            if (!QuestIsProfileWhitelisted(profile.Info.GameVersion, quest.Id))
            {
                continue;
            }

            if (!ShowEventQuestToPlayer(quest.Id))
            {
                continue;
            }

            // Don't add quests that have a level higher than the user's
            if (!PlayerLevelFulfillsQuestRequirement(quest, profile.Info.Level!.Value))
            {
                continue;
            }

            // Player can use trader mods then remove them, leaving quests behind
            if (!profile.TradersInfo!.ContainsKey(quest.TraderId))
            {
                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug($"Unable to show quest: {quest.Name} as its for a trader: {quest.TraderId} that no longer exists.");
                }

                continue;
            }

            // Gated on a note, tape or patch the player has not read yet, or on story progress the
            // client would otherwise evaluate itself and show as a locked quest
            if (!CompletableItemRequirementsMet(quest, profile) || !GlobalVariableRequirementsMet(quest, profile))
            {
                continue;
            }

            // The raid starts these itself when the item is read, this catches a raid that was quit before then
            if (AutoStartConditionsMet(quest, profile))
            {
                quest.SptStatus = StartStorylineQuest(profile, quest.Id);
                questsToShowPlayer.Add(quest);
                continue;
            }

            var questRequirements = quest.Conditions.AvailableForStart!.GetQuestConditions();
            var loyaltyRequirements = quest.Conditions.AvailableForStart!.GetLoyaltyConditions();
            var standingRequirements = quest.Conditions.AvailableForStart!.GetStandingConditions();

            // Quest has no conditions, standing or loyalty conditions, add to visible quest list
            if (questRequirements.Count == 0 && loyaltyRequirements.Count == 0 && standingRequirements.Count == 0)
            {
                var storyStatus = ResolveStorylineStatus(profile, quest, false);
                if (storyStatus is null)
                {
                    continue;
                }

                quest.SptStatus = storyStatus.Value;
                questsToShowPlayer.Add(quest);
                continue;
            }

            // Check the status of each quest condition, if any are not completed
            // then this quest should not be visible
            var haveCompletedPreviousQuest = true;
            foreach (var conditionToFulfil in questRequirements)
            {
                // If the previous quest isn't in the user profile, it hasn't been completed or started
                var questIdsToFulfil =
                    (
                        conditionToFulfil.Target!.IsList ? conditionToFulfil.Target.List
                        : conditionToFulfil.Target.Item == null ? null
                        : [conditionToFulfil.Target.Item]
                    ) ?? [];
                var prerequisiteQuest = profile.Quests!.FirstOrDefault(profileQuest => questIdsToFulfil.Contains(profileQuest.QId));

                if (prerequisiteQuest is null)
                {
                    haveCompletedPreviousQuest = false;
                    break;
                }

                // Prereq does not have its status requirement fulfilled
                // Some bsg status ids are strings, MUST convert to number before doing includes check
                if (!conditionToFulfil.Status!.Contains(prerequisiteQuest.Status))
                {
                    haveCompletedPreviousQuest = false;
                    break;
                }

                // Has a wait timer
                if (conditionToFulfil.AvailableAfter > 0)
                {
                    // Compare current time to unlock time for previous quest
                    prerequisiteQuest.StatusTimers.TryGetValue(prerequisiteQuest.Status, out var previousQuestCompleteTime);
                    var unlockTime = previousQuestCompleteTime + conditionToFulfil.AvailableAfter;
                    if (unlockTime > timeUtil.GetTimeStamp())
                    {
                        logger.Debug($"Quest {quest.Name} is locked for another: {unlockTime - timeUtil.GetTimeStamp()} seconds");
                    }
                }
            }

            // Previous quest not completed, skip
            if (!haveCompletedPreviousQuest)
            {
                continue;
            }

            var passesLoyaltyRequirements = true;
            foreach (var condition in loyaltyRequirements)
            {
                if (!TraderLoyaltyLevelRequirementCheck(condition, profile))
                {
                    passesLoyaltyRequirements = false;
                    break;
                }
            }

            var passesStandingRequirements = true;
            foreach (var condition in standingRequirements)
            {
                if (!TraderStandingRequirementCheck(condition, profile))
                {
                    passesStandingRequirements = false;
                    break;
                }
            }

            if (haveCompletedPreviousQuest && passesLoyaltyRequirements && passesStandingRequirements)
            {
                var storyStatus = ResolveStorylineStatus(profile, quest, questRequirements.Count > 0);
                if (storyStatus is null)
                {
                    continue;
                }

                quest.SptStatus = storyStatus.Value;
                questsToShowPlayer.Add(quest);
            }
        }

        // A chapter started by one of its tasks above may have been skipped earlier in the loop
        foreach (var profileQuest in profile.Quests)
        {
            if (!IsStoryChapter(profileQuest.QId) || questsToShowPlayer.Any(shown => shown.Id == profileQuest.QId))
            {
                continue;
            }

            var chapter = allQuests.FirstOrDefault(quest => quest.Id == profileQuest.QId);
            if (chapter is null)
            {
                continue;
            }

            chapter.SptStatus = profileQuest.Status;
            questsToShowPlayer.Add(chapter);
        }

        return UpdateQuestsForGameEdition(cloner.Clone(questsToShowPlayer)!, profile.Info!.GameVersion!);
    }

    protected static bool CompletableItemRequirementsMet(Models.Eft.Common.Tables.Quest quest, PmcData profile)
    {
        return (quest.Conditions?.AvailableForStart ?? [])
            .Where(condition => condition.ConditionType == "CompletableItem" && condition.Target?.Item is not null)
            .All(condition => profile.CompletableItems?.GetValueOrDefault(condition.Target!.Item!) ?? false);
    }

    protected static bool AutoStartConditionsMet(Models.Eft.Common.Tables.Quest quest, PmcData profile)
    {
        var conditions = (quest.Conditions?.AutoStart ?? [])
            .Where(condition => condition.ConditionType == "CompletableItem" && condition.Target?.Item is not null)
            .ToList();

        return conditions.Count > 0
            && conditions.All(condition => profile.CompletableItems?.GetValueOrDefault(condition.Target!.Item!) ?? false);
    }

    /// <summary>A finished quest with a note, tape or patch among its objectives means that item was read.</summary>
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

    protected bool GlobalVariableRequirementsMet(Models.Eft.Common.Tables.Quest quest, PmcData profile)
    {
        return (quest.Conditions?.AvailableForStart ?? [])
            .Where(condition => condition.ConditionType == "GlobalVariableValue" && condition.Target?.Item is not null)
            .All(condition =>
            {
                var current = GetVariableValue(profile, condition.Target!.Item!);
                var required = condition.Value ?? 0;

                return condition.CompareMethod switch
                {
                    "==" => current == required,
                    ">" => current > required,
                    _ => current >= required,
                };
            });
    }

    // A condition may name a variable group, whose value is the sum of its member variables. Trader
    // dialogue and quest rewards set the members, so a trader's side quests open up as the story advances.
    protected int GetVariableValue(PmcData profile, MongoId variableId)
    {
        var variables = profile.Variables ?? [];
        var group = templateTable.VariableGroups.FirstOrDefault(group => group.Id == variableId);

        return group is null ? variables.GetValueOrDefault(variableId) : group.Variables.Sum(member => variables.GetValueOrDefault(member));
    }

    // Live starts the Tour chapter itself seconds after the character is created. Every other chapter
    // starts together with the first of its tasks unlocked by a finished quest, and stays hidden until then.
    // Hidden quests have no trader screen to accept them from, so they start the moment they unlock.
    // Returns the status a quest should be shown with, or null when it must stay hidden.
    protected QuestStatusEnum? ResolveStorylineStatus(PmcData profile, Models.Eft.Common.Tables.Quest quest, bool unlockedByQuest)
    {
        var status = ResolveChapterStatus(profile, quest, unlockedByQuest);
        if (status == QuestStatusEnum.AvailableForStart && quest.NotDisplayedQuest == true)
        {
            return StartStorylineQuest(profile, quest.Id);
        }

        return status;
    }

    protected QuestStatusEnum? ResolveChapterStatus(PmcData profile, Models.Eft.Common.Tables.Quest quest, bool unlockedByQuest)
    {
        if (IsStoryChapter(quest.Id))
        {
            return quest.Id == _guideChapterId ? StartStorylineQuest(profile, quest.Id) : null;
        }

        var chapterId = ChapterOf(quest.Id);
        if (chapterId is null)
        {
            return QuestStatusEnum.AvailableForStart;
        }

        var chapterStarted =
            profile.Quests?.Any(profileQuest => profileQuest.QId == chapterId && profileQuest.Status == QuestStatusEnum.Started) ?? false;

        // A finished starter task opens its chapter and the chapter's first task in the same moment, the
        // rest of the tasks not gated on another quest wait for the player once their chapter is running
        if (!unlockedByQuest)
        {
            if (!chapterStarted && !StarterCompleted(profile, chapterId.Value))
            {
                return null;
            }

            StartStorylineQuest(profile, chapterId.Value);

            return quest.Id == FirstTaskOf(chapterId.Value) && StarterCompleted(profile, chapterId.Value)
                ? StartStorylineQuest(profile, quest.Id)
                : QuestStatusEnum.AvailableForStart;
        }

        // A task the player accepts in a trader dialogue waits there, and so does its chapter
        if (AcceptedInDialogue(quest.Id))
        {
            return QuestStatusEnum.AvailableForStart;
        }

        if (!chapterStarted)
        {
            StartStorylineQuest(profile, chapterId.Value);
        }

        return StartStorylineQuest(profile, quest.Id);
    }

    /// <summary>A task that is active by any route, a raid, the client or an accepted dialogue, has started its chapter.</summary>
    protected void StartChapterOfActiveTask(PmcData profile, MongoId taskId, QuestStatusEnum status)
    {
        if (status is QuestStatusEnum.Locked or QuestStatusEnum.AvailableForStart)
        {
            return;
        }

        var chapterId = ChapterOf(taskId);
        if (chapterId is not null && !profile.Quests!.Any(profileQuest => profileQuest.QId == chapterId))
        {
            StartStorylineQuest(profile, chapterId.Value);
        }
    }

    /// <summary>Quests an AcceptQuest dialogue action hands out.</summary>
    protected bool AcceptedInDialogue(MongoId questId)
    {
        _dialogueAcceptedQuests ??= templateTable
            .Dialogue.Elements.SelectMany(element => element.Lines)
            .SelectMany(line => line.Actions)
            .Where(action => action.Type == "AcceptQuest" && action.QuestId is not null)
            .Select(action => action.QuestId!.Value)
            .ToHashSet();

        return _dialogueAcceptedQuests.Contains(questId);
    }

    protected QuestStatusEnum StartStorylineQuest(PmcData profile, MongoId questId)
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

        return QuestStatusEnum.Started;
    }

    /// <summary>A chapter is a quest the story rail is built from, named by the main quest notes.</summary>
    protected bool IsStoryChapter(MongoId questId)
    {
        return templateTable.MainQuestNotes.Any(note => note.ChapterId == questId);
    }

    /// <summary>The chapter that lists this quest as one of its tasks, if any.</summary>
    protected MongoId? ChapterOf(MongoId taskId)
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
    ///     A hidden task that fails when its chapter is already running is that chapter's starter, the visit to
    ///     the Labyrinth transit or Batya's camp. Live starts the chapter the second the starter completes.
    /// </summary>
    protected bool StarterCompleted(PmcData profile, MongoId chapterId)
    {
        _chapterStarters ??= BuildChapterStarters();

        return _chapterStarters.TryGetValue(chapterId, out var starters)
            && (
                profile.Quests?.Any(profileQuest => starters.Contains(profileQuest.QId) && profileQuest.Status == QuestStatusEnum.Success)
                ?? false
            );
    }

    protected MongoId? FirstTaskOf(MongoId chapterId)
    {
        var first = templateTable
            .Quests.GetValueOrDefault(chapterId)
            ?.Conditions?.AvailableForFinish?.FirstOrDefault(condition =>
                condition.ConditionType == "Quest" && condition.Target?.Item is not null
            );

        return first is null ? null : new MongoId(first.Target!.Item!);
    }

    private Dictionary<MongoId, HashSet<MongoId>> BuildChapterStarters()
    {
        var starters = new Dictionary<MongoId, HashSet<MongoId>>();
        foreach (var (questId, quest) in templateTable.Quests)
        {
            foreach (var condition in (quest.Conditions?.Fail ?? []).Where(c => c.ConditionType == "Quest" && c.Target?.Item is not null))
            {
                var chapterId = new MongoId(condition.Target!.Item!);
                if (IsStoryChapter(chapterId))
                {
                    starters.TryAdd(chapterId, []);
                    starters[chapterId].Add(questId);
                }
            }
        }

        return starters;
    }

    private Dictionary<MongoId, MongoId> BuildTaskChapters()
    {
        return templateTable
            .MainQuestNotes.Select(note => new MongoId(note.ChapterId))
            .Distinct()
            .SelectMany(chapterId =>
                (templateTable.Quests.GetValueOrDefault(chapterId)?.Conditions?.AvailableForFinish ?? [])
                    .Where(condition => condition.ConditionType == "Quest" && condition.Target?.Item is not null)
                    .Select(condition => (Task: new MongoId(condition.Target!.Item!), Chapter: chapterId))
            )
            .DistinctBy(pair => pair.Task)
            .ToDictionary(pair => pair.Task, pair => pair.Chapter);
    }

    /// <summary>
    /// Remove rewards from quests that do not fulfil the gameversion requirement
    /// </summary>
    /// <param name="quests">List of quests to check</param>
    /// <param name="gameVersion">Game version of the profile</param>
    /// <returns>Collection of Quest objects with the rewards filtered correctly for the game version</returns>
    protected List<Models.Eft.Common.Tables.Quest> UpdateQuestsForGameEdition(
        List<Models.Eft.Common.Tables.Quest> quests,
        string gameVersion
    )
    {
        foreach (var quest in quests)
        {
            // Remove any reward that doesn't pass the game edition check
            foreach (var rewardType in quest.Rewards!)
            {
                if (rewardType.Value is null)
                {
                    continue;
                }

                quest.Rewards[rewardType.Key] = quest.Rewards[rewardType.Key].ToList();
            }
        }

        return quests;
    }

    /// <summary>
    /// Return a list of quests that would fail when supplied quest is completed
    /// </summary>
    /// <param name="completedQuestId">Quest completed id</param>
    /// <param name="pmcProfile"></param>
    /// <returns>Collection of Quest objects</returns>
    protected List<Models.Eft.Common.Tables.Quest> GetQuestsFromProfileFailedByCompletingQuest(MongoId completedQuestId, PmcData pmcProfile)
    {
        var questsInDb = GetQuestsFromDb();
        return questsInDb
            .Where(quest =>
            {
                // No fail conditions, skip
                if (quest.Conditions.Fail is null || quest.Conditions.Fail.Count == 0)
                {
                    return false;
                }

                // Quest already exists in profile and is failed, skip
                if (pmcProfile.Quests!.Any(profileQuest => profileQuest.QId == quest.Id && profileQuest.Status == QuestStatusEnum.Fail))
                {
                    return false;
                }

                // Check if completed quest is inside iterated quests fail conditions
                foreach (var condition in quest.Conditions.Fail)
                {
                    // No target, cant be failed by our completed quest
                    if (condition.Target is null)
                    {
                        continue;
                    }

                    // 'Target' property can be Collection or string, handle each differently
                    if (condition.Target.IsList && condition.Target.List!.Contains(completedQuestId))
                    {
                        // Check if completed quest id exists in fail condition
                        return true;
                    }

                    if (condition.Target.IsItem && condition.Target.Item! == completedQuestId)
                    {
                        // Not a list, plain string
                        return true;
                    }
                }

                return false;
            })
            .ToList();
    }

    /// <summary>
    /// Fail the provided quests - Update quest in profile, otherwise add fresh quest object with failed status
    /// </summary>
    /// <param name="sessionId">session id</param>
    /// <param name="pmcData">player profile</param>
    /// <param name="questsToFail">quests to fail</param>
    /// <param name="output">Client output</param>
    protected void FailQuests(
        MongoId sessionId,
        PmcData pmcData,
        List<Models.Eft.Common.Tables.Quest> questsToFail,
        ItemEventRouterResponse output
    )
    {
        foreach (var questToFail in questsToFail)
        {
            // Skip failing a quest that has a fail status of something other than success
            if (questToFail.Conditions.Fail?.Any(x => x.Status?.Any(status => status != QuestStatusEnum.Success) ?? false) ?? false)
            {
                continue;
            }

            var isActiveQuestInPlayerProfile = pmcData.Quests!.FirstOrDefault(quest => quest.QId == questToFail.Id);
            if (isActiveQuestInPlayerProfile is not null)
            {
                if (isActiveQuestInPlayerProfile.Status != QuestStatusEnum.Fail)
                {
                    var failBody = new FailQuestRequestData
                    {
                        Action = "QuestFail",
                        QuestId = questToFail.Id,
                        RemoveExcessItems = true,
                    };
                    FailQuest(pmcData, failBody, sessionId, output);
                }
            }
            else
            {
                // Failing an entirely new quest that doesn't exist in profile
                var statusTimers = new Dictionary<QuestStatusEnum, double>();

                if (!statusTimers.TryGetValue(QuestStatusEnum.Fail, out _))
                {
                    statusTimers.Add(QuestStatusEnum.Fail, 0);
                }

                statusTimers[QuestStatusEnum.Fail] = timeUtil.GetTimeStamp();
                var questData = new QuestStatus
                {
                    QId = questToFail.Id,
                    StartTime = timeUtil.GetTimeStamp(),
                    StatusTimers = statusTimers,
                    Status = QuestStatusEnum.Fail,
                };
                pmcData.Quests!.Add(questData);
            }
        }
    }

    /// <summary>
    /// Send a popup to player on successful completion of a quest
    /// </summary>
    /// <param name="sessionId">session id</param>
    /// <param name="pmcData">Player profile</param>
    /// <param name="completedQuestId">Completed quest id</param>
    /// <param name="questRewards">Rewards given to player</param>
    protected void SendSuccessDialogMessageOnQuestComplete(
        MongoId sessionId,
        PmcData pmcData,
        MongoId completedQuestId,
        List<Item> questRewards
    )
    {
        var quest = GetQuestFromDb(completedQuestId, pmcData);
        if (quest is null)
        {
            return;
        }

        if (!QuestMessageIsWorthSending(quest, quest.SuccessMessageText, questRewards))
        {
            return;
        }

        mailSendService.SendLocalisedNpcMessageToPlayer(
            sessionId,
            quest.TraderId,
            MessageType.QuestSuccess,
            quest.SuccessMessageText ?? "",
            questRewards,
            timeUtil.GetHoursAsSeconds((int)GetMailItemRedeemTimeHoursForProfile(pmcData))
        );
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
    /// Look for newly available quests after completing a quest with a requirement to wait x minutes (time-locked) before being available and add data to profile
    /// </summary>
    /// <param name="pmcData">Player profile to update</param>
    /// <param name="quests">Quests to look for wait conditions in</param>
    /// <param name="completedQuestId">Quest just completed</param>
    protected void AddTimeLockedQuestsToProfile(
        PmcData pmcData,
        IEnumerable<Models.Eft.Common.Tables.Quest> quests,
        MongoId completedQuestId
    )
    {
        // Iterate over quests, look for quests with right criteria
        foreach (var quest in quests)
        {
            // If quest has prereq of completed quest + availableAfter value > 0 (quest has wait time)
            var nextQuestWaitCondition = quest.Conditions.AvailableForStart?.FirstOrDefault(x =>
                ((x.Target?.List?.Contains(completedQuestId) ?? false) || (x.Target?.Item?.Contains(completedQuestId) ?? false))
                && x.AvailableAfter > 0
            ); // as we have to use the ListOrT type now, check both List and Item for the above checks

            if (nextQuestWaitCondition is not null)
            {
                // Now + wait time
                var availableAfterTimestamp = timeUtil.GetTimeStamp() + nextQuestWaitCondition.AvailableAfter;

                // Update quest in profile with status of AvailableAfter
                var existingQuestInProfile = pmcData.Quests!.FirstOrDefault(x => x.QId == quest.Id);
                if (existingQuestInProfile is not null)
                {
                    existingQuestInProfile.AvailableAfter = availableAfterTimestamp;
                    existingQuestInProfile.Status = QuestStatusEnum.AvailableAfter;
                    existingQuestInProfile.StartTime = 0;
                    existingQuestInProfile.StatusTimers = new Dictionary<QuestStatusEnum, double>();

                    continue;
                }

                pmcData.Quests!.Add(
                    new QuestStatus
                    {
                        QId = quest.Id,
                        StartTime = 0,
                        Status = QuestStatusEnum.AvailableAfter,
                        StatusTimers = new Dictionary<QuestStatusEnum, double>
                        {
                            { QuestStatusEnum.AvailableAfter, timeUtil.GetTimeStamp() },
                        },
                        AvailableAfter = availableAfterTimestamp,
                    }
                );
            }
        }
    }

    /// <summary>
    /// Remove a quest entirely from a profile
    /// </summary>
    /// <param name="sessionId">Player id</param>
    /// <param name="questIdToRemove">Qid of quest to remove</param>
    protected void RemoveQuestFromScavProfile(MongoId sessionId, MongoId questIdToRemove)
    {
        var fullProfile = profileHelper.GetFullProfile(sessionId);
        var repeatableInScavProfile = fullProfile.CharacterData?.ScavData?.Quests?.FirstOrDefault(x => x.QId == questIdToRemove);
        if (repeatableInScavProfile is null)
        {
            logger.Warning(
                serverLocalisationService.GetText(
                    "quest-unable_to_remove_scav_quest_from_profile",
                    new { scavQuestId = questIdToRemove, profileId = sessionId }
                )
            );

            return;
        }

        fullProfile.CharacterData!.ScavData!.Quests!.Remove(repeatableInScavProfile);
    }

    /// <summary>
    /// Get quests that have different statuses
    /// </summary>
    /// <param name="preQuestStatuses">Quests before</param>
    /// <param name="postQuestStatuses">Quests after</param>
    /// <returns>QuestStatusChange array</returns>
    protected List<QuestStatus> GetQuestsWithDifferentStatuses(List<QuestStatus> preQuestStatuses, List<QuestStatus> postQuestStatuses)
    {
        List<QuestStatus> result = [];

        foreach (var quest in postQuestStatuses)
        {
            // Add quest if status differs or quest not found
            var preQuest = preQuestStatuses.FirstOrDefault(x => x.QId == quest.QId);
            if (preQuest is null || preQuest.Status != quest.Status)
            {
                result.Add(quest);
            }
        }

        return result;
    }

    /// <summary>
    /// Does a provided quest have a level requirement equal to or below defined level
    /// </summary>
    /// <param name="quest">Quest to check</param>
    /// <param name="playerLevel">level of player to test against quest</param>
    /// <returns>true if quest can be seen/accepted by player of defined level</returns>
    protected bool PlayerLevelFulfillsQuestRequirement(Models.Eft.Common.Tables.Quest quest, int playerLevel)
    {
        var levelConditions = quest.Conditions.AvailableForStart?.GetLevelConditions();
        if (levelConditions is not null)
        {
            foreach (var levelCondition in levelConditions)
            {
                if (!DoesPlayerLevelFulfilCondition(playerLevel, levelCondition))
                // Not valid, exit out
                {
                    return false;
                }
            }
        }

        // All conditions passed / has no level requirement, valid
        return true;
    }
}
