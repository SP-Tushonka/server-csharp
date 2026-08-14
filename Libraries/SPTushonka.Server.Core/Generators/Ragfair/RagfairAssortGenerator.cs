using System.Collections.Frozen;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SPTarkov.Server.Core.Generators.Ragfair;

[Injectable]
public class RagfairAssortGenerator(
    TemplateTable templateTable,
    ItemHelper itemHelper,
    PresetHelper presetHelper,
    SeasonalEventService seasonalEventService,
    ItemFilterService itemFilterService,
    RagfairConfig ragfairConfig,
    ICloner cloner
)
{
    protected readonly FrozenSet<MongoId> RagfairItemInvalidBaseTypes =
    [
        BaseClasses.LOOT_CONTAINER, // Safe, barrel cache etc
        BaseClasses.STASH, // Player inventory stash
        BaseClasses.SORTING_TABLE,
        BaseClasses.INVENTORY,
        BaseClasses.STATIONARY_CONTAINER,
        BaseClasses.POCKETS,
        BaseClasses.BUILT_IN_INSERTS,
    ];

    /// <summary>
    ///     Generate a list of lists (item + children) the flea can sell
    /// </summary>
    /// <returns> List of lists (item + children)</returns>
    public IEnumerable<List<Item>> GenerateRagfairAssortItems()
    {
        var results = new List<List<Item>>(templateTable.Items.Count);

        var blacklist = itemFilterService.GetBlacklistedItems();

        // Store processed preset tpls so we don't add them when processing non-preset items
        HashSet<MongoId> processedArmorItems = [];
        var skipOutOfSeasonItems = ragfairConfig.Dynamic.RemoveSeasonalItemsWhenNotInEvent && !seasonalEventService.SeasonalEventEnabled();
        var seasonalItemTplBlacklist = seasonalEventService.GetInactiveSeasonalEventItems();

        foreach (var preset in GetPresetsToAdd())
        {
            // Update Ids and clone
            var presetAndModsClone = cloner.Clone(preset.Items).ReplaceIDs().ToList();
            presetAndModsClone.RemapRootItemId();

            // Add presets base item tpl to the processed list so its skipped later on when processing items
            processedArmorItems.Add(preset.Items[0].Template);

            var presetRoot = presetAndModsClone[0];
            presetRoot.ParentId = "hideout";
            presetRoot.SlotId = "hideout";
            presetRoot.Upd = new Upd
            {
                StackObjectsCount = 99999999,
                UnlimitedCount = true,
                SptPresetId = preset.Id,
            };

            results.Add(presetAndModsClone);
        }

        foreach (var (tpl, item) in templateTable.Items)
        {
            // Already processed as a preset
            if (processedArmorItems.Contains(tpl))
            {
                continue;
            }

            if (string.Equals(item.Type, "Node", StringComparison.OrdinalIgnoreCase) || blacklist.Contains(tpl))
            {
                continue;
            }

            // Skip seasonal items when not in-season
            if (skipOutOfSeasonItems && seasonalItemTplBlacklist.Contains(tpl))
            {
                continue;
            }
            
            if (!itemHelper.IsValidItem(item, RagfairItemInvalidBaseTypes))
            {
                continue;
            }

            results.Add([CreateRagfairAssortRootItem(tpl, tpl)]); // tpl and id must be the same so hideout recipe rewards work
        }

        return results;
    }

    /// <summary>
    ///     Get presets from globals to add to flea. <br />
    ///     ragfairConfig.dynamic.showDefaultPresetsOnly decides if it's all presets or just defaults
    /// </summary>
    /// <returns> List of Preset </returns>
    protected List<Preset> GetPresetsToAdd()
    {
        return ragfairConfig.Dynamic.ShowDefaultPresetsOnly
            ? presetHelper.GetDefaultPresets().Values.ToList()
            : presetHelper.GetAllPresets();
    }

    /// <summary>
    ///     Create a base assort item and return it with populated values + 999999 stack count + unlimited count = true
    /// </summary>
    /// <param name="tplId"> tplId to add to item </param>
    /// <param name="id"> id to add to item </param>
    /// <returns> Hydrated Item object </returns>
    protected Item CreateRagfairAssortRootItem(MongoId tplId, MongoId? id = null)
    {
        if (id == null || id.Value.IsEmpty)
        {
            id = new MongoId();
        }

        return new Item
        {
            Id = id.Value,
            Template = tplId,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd { StackObjectsCount = 99999999, UnlimitedCount = true },
        };
    }
}
