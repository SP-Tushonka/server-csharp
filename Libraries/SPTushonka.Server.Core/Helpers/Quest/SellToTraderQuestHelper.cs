using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;

namespace SPTarkov.Server.Core.Helpers.Quest;

/// <summary>
///     Counts items sold to traders towards the quests that ask for them
/// </summary>
[Injectable(InjectionType.Singleton)]
public class SellToTraderQuestHelper(
    ISptLogger<SellToTraderQuestHelper> logger,
    TemplateTable templateTable,
    ServerLocalisationService serverLocalisationService
)
{
    /// <summary>
    /// List of <see cref="Quest"/> conditions that require trader sales be tracked and incremented, keyed by <see cref="Quest.Id"/>
    /// We need to keep track of quests with `SellItemToTrader` finish conditions to avoid expensive lookups during trading.
    /// </summary>
    protected virtual Dictionary<MongoId, List<QuestCondition>> SellToTraderQuestConditionCache
    {
        get { return field ??= GetSellToTraderQuests(templateTable.Quests.Values); }
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
}
