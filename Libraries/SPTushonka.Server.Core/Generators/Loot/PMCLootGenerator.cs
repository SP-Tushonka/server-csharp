using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Services.Server;

namespace SPTarkov.Server.Core.Generators.Loot;

[Injectable(InjectionType.Singleton)]
public class PMCLootGenerator(
    TemplateTable templateTable,
    BotTable botTable,
    LocationTable locationTable,
    ItemHelper itemHelper,
    ItemFilterService itemFilterService,
    RagfairPriceService ragfairPriceService,
    SeasonalEventService seasonalEventService,
    WeightedRandomHelper weightedRandomHelper,
    PmcConfig pmcConfig
)
{
    // Store loot against its type, usec/bear
    private readonly Dictionary<string, Dictionary<MongoId, double>> _backpackLootPool = [];
    private readonly Dictionary<string, Dictionary<MongoId, double>> _pocketLootPool = [];
    private readonly Dictionary<string, Dictionary<MongoId, double>> _vestLootPool = [];

    protected readonly Lock BackpackLock = new();
    protected readonly Lock PocketLock = new();
    protected readonly Lock VestLock = new();

    /// <summary>
    ///     Create a List of loot items a PMC can have in their pockets
    /// </summary>
    /// <param name="pmcRole">Role of PMC having loot generated (bear or usec)</param>
    /// <returns>Dictionary of string and number</returns>
    public Dictionary<MongoId, double> GeneratePMCPocketLootPool(string pmcRole)
    {
        lock (PocketLock)
        {
            // Already exists, return values
            if (_pocketLootPool.TryGetValue(pmcRole, out var existingLootPool))
            {
                return existingLootPool;
            }

            // Get a set of item types we want to generate
            var allowedItemTypeWhitelist = pmcConfig.PocketLoot.Whitelist;

            // Get a set of ids we don't want to generate
            var blacklist = GetContainerLootBlacklist();

            // Generate loot and cache - Also pass check to ensure only 1x2 items are allowed (Unheard bots have big pockets, hence the need for 1x2)
            var pool = GenerateLootPool(pmcRole, allowedItemTypeWhitelist, blacklist, ItemFitsInto1By2Slot);
            _pocketLootPool.TryAdd(pmcRole, pool);

            return pool;
        }
    }

    /// <summary>
    ///     Create a dictionary of loot items a PMC can have in their vests with a corresponding weight of being picked to spawn
    /// </summary>
    /// <param name="pmcRole">Role of PMC having loot generated (bear or usec)</param>
    /// <returns>Dictionary item template ids and a weighted chance of being picked</returns>
    public Dictionary<MongoId, double> GeneratePMCVestLootPool(string pmcRole)
    {
        lock (VestLock)
        {
            // Already exists, return values
            if (_vestLootPool.TryGetValue(pmcRole, out var existingLootPool))
            {
                return existingLootPool;
            }

            // Get a set of item types we want to generate
            var allowedItemTypeWhitelist = pmcConfig.VestLoot.Whitelist;

            // Get a set of ids we don't want to generate
            var blacklist = GetContainerLootBlacklist();
            blacklist.UnionWith(pmcConfig.VestLoot.Blacklist); // Include vest-specific blacklist

            // Generate loot and cache - Also pass check to ensure items up to 2x2 are allowed, some vests have big slots
            var pool = GenerateLootPool(pmcRole, allowedItemTypeWhitelist, blacklist, ItemFitsInto2By2Slot);
            _vestLootPool.TryAdd(pmcRole, pool);

            return pool;
        }
    }

    /// <summary>
    ///     Create a List of loot items a PMC can have in their backpack
    /// </summary>
    /// <param name="pmcRole">Role of PMC having loot generated (bear or usec)</param>
    /// <returns>Dictionary of string and number</returns>
    public Dictionary<MongoId, double> GeneratePMCBackpackLootPool(string pmcRole)
    {
        lock (BackpackLock)
        {
            // Already exists, return values
            if (_backpackLootPool.TryGetValue(pmcRole, out var existingLootPool))
            {
                return existingLootPool;
            }

            var allowedItemTypeWhitelist = pmcConfig.BackpackLoot.Whitelist;
            var blacklist = GetContainerLootBlacklist();
            blacklist.UnionWith(pmcConfig.BackpackLoot.Blacklist); // Include backpack-specific blacklist

            // Generate loot and cache
            var pool = GenerateLootPool(pmcRole, allowedItemTypeWhitelist, blacklist, null);
            _backpackLootPool.TryAdd(pmcRole, pool);

            return pool;
        }
    }

    /// <summary>
    /// Helper method to generate a loot pool of item tpls based on the inputs provided
    /// </summary>
    /// <param name="pmcRole">Role of PMC to generate loot for (pmcBEAR or pmcUSEC)</param>
    /// <param name="allowedItemTypeWhitelist">A list of item types the pmc can spawn</param>
    /// <param name="itemTplAndParentBlacklist">Item and parent blacklist</param>
    /// <param name="genericItemCheck">An optional delegate to validate the TemplateItem object being processed</param>
    /// <returns>Dictionary of items and weights inversely tied to the items price</returns>
    protected Dictionary<MongoId, double> GenerateLootPool(
        string pmcRole,
        HashSet<MongoId> allowedItemTypeWhitelist,
        HashSet<MongoId> itemTplAndParentBlacklist,
        Func<TemplateItem, bool>? genericItemCheck
    )
    {
        var lootPool = new Dictionary<MongoId, double>();
        var items = templateTable.Items;

        // Filter all items in DB to ones we want with passed in whitelist + blacklist + generic 'IsValidItem' check
        // Also run Delegate if it's not null
        var itemTplsToAdd = items
            .Where(item =>
                allowedItemTypeWhitelist.Contains(item.Value.Parent)
                && itemHelper.IsValidItem(item.Value.Id)
                && !itemTplAndParentBlacklist.Contains(item.Value.Id)
                && !itemTplAndParentBlacklist.Contains(item.Value.Parent)
                && (genericItemCheck?.Invoke(item.Value) ?? true) // if delegate is null, force check to be true
            )
            .Select(x => x.Key);

        // Store all items + price in above lootPool dictionary
        foreach (var tpl in itemTplsToAdd)
        {
            // If PMC has price override, use that. Otherwise, use flea price
            lootPool.TryAdd(tpl, GetItemPrice(tpl));
        }

        // Get the highest priced item being stored in loot pool
        var highestPrice = lootPool.Max(price => price.Value);
        foreach (var (key, _) in lootPool)
        // Invert price so cheapest has a larger weight
        // Times by highest price so most expensive item has weight of 1
        {
            // This results in cheap items having higher weighting and thus a higher chance of being picked
            lootPool[key] = Math.Round(1 / lootPool[key] * highestPrice);
        }

        // Get the greatest common divisor for all items in pool, use it to reduce the weight value and get more readable numbers
        weightedRandomHelper.ReduceWeightValues(lootPool);

        return lootPool;
    }

    /// <summary>
    /// Get a generic all-container blacklist
    /// </summary>
    /// <returns>Hashset of blacklisted items</returns>
    protected HashSet<MongoId> GetContainerLootBlacklist()
    {
        var blacklist = new HashSet<MongoId>();
        blacklist.UnionWith(pmcConfig.PocketLoot.Blacklist);
        blacklist.UnionWith(pmcConfig.GlobalLootBlacklist);
        blacklist.UnionWith(itemFilterService.GetBlacklistedItems());
        blacklist.UnionWith(itemFilterService.GetItemRewardBlacklist());
        blacklist.UnionWith(itemFilterService.GetBlacklistedLootableItems());
        blacklist.UnionWith(seasonalEventService.GetInactiveSeasonalEventItems());
        blacklist.UnionWith(GetSecretExitItems());

        return blacklist;
    }

    protected HashSet<MongoId> GetSecretExitItems()
    {
        var items = new HashSet<MongoId>();
        foreach (var location in locationTable.GetDictionary().Values)
        {
            foreach (var exit in location.Base?.SecretExits ?? [])
            {
                if (MongoId.IsValidMongoId(exit.Id))
                {
                    items.Add(exit.Id);
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Get an items price from db or override if it exists
    /// </summary>
    /// <param name="tpl">Item tpl to get price of</param>
    /// <returns>Rouble price</returns>
    protected double GetItemPrice(MongoId tpl)
    {
        // Store items price so we can turn it into a weighting later
        return ragfairPriceService.GetDynamicItemPrice(tpl, Money.ROUBLES) ?? 0;
    }

    /// <summary>
    ///     Check if item has a width/height that lets it fit into a 2x2 slot
    ///     1x1 / 1x2 / 2x1 / 2x2
    /// </summary>
    /// <param name="item">Item to check size of</param>
    /// <returns>true if it fits</returns>
    protected bool ItemFitsInto2By2Slot(TemplateItem item)
    {
        return item.Properties.Width <= 2 && item.Properties.Height <= 2;
    }

    /// <summary>
    ///     Check if item has a width/height that lets it fit into a 1x2 slot
    ///     1x1 / 1x2 / 2x1
    /// </summary>
    /// <param name="item">Item to check size of</param>
    /// <returns>true if it fits</returns>
    protected bool ItemFitsInto1By2Slot(TemplateItem item)
    {
        return $"{item.Properties.Width}x{item.Properties.Height}" switch
        {
            "1x1" or "1x2" or "2x1" => true,
            _ => false,
        };
    }
}
