using LootDumpProcessor.Model.Input;
using LootDumpProcessor.Model.Processing;
using LootDumpProcessor.Utils;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LootDumpProcessor.Process.Processor;

public static class StaticLootProcessor
{
    public static List<PreProcessedStaticLoot> PreProcessStaticLoot(List<SpawnpointTemplate> staticloot)
    {
        var containers = new List<PreProcessedStaticLoot>();
        foreach (var lootSpawnPosition in staticloot)
        {
            var container = lootSpawnPosition.Items.First();
            if (!LootDumpProcessorContext.GetStaticWeaponIds().Contains(container.Template))
            {
                // Only add non-weapon static containers
                containers.Add(
                    new PreProcessedStaticLoot
                    {
                        Type = container.Template,
                        ContainerId = container.Id,
                        Items = lootSpawnPosition.Items.Skip(1).ToList(),
                    }
                );
            }
        }

        return containers;
    }

    public static Tuple<string, StaticContainerDetails> CreateStaticWeaponsAndStaticForcedContainers(Data rawMapDump)
    {
        var mapId = rawMapDump.LocationLoot.Id.ToLower();
        var staticLootPositions = (from li in rawMapDump.LocationLoot.Loot where li.IsContainer ?? false select li).ToList();
        var staticWeapons = new List<SpawnpointTemplate>();
        staticLootPositions = staticLootPositions.OrderBy(x => x.Id).ToList();
        foreach (var staticLootPosition in staticLootPositions)
        {
            if (LootDumpProcessorContext.GetStaticWeaponIds().Contains(staticLootPosition.Items.First().Template))
            {
                staticWeapons.Add(staticLootPosition.DeepClone());
            }
        }

        var forcedStaticItems = LootDumpProcessorContext.GetForcedItems().TryGetValue(mapId, out var forcedEntries)
            ? forcedEntries.Select(entry => new StaticForced { ContainerId = entry.ContainerId, ItemTpl = entry.ItemTpl }).ToList()
            : new List<StaticForced>();

        var mapStaticData = new StaticContainerDetails { StaticWeapons = staticWeapons, StaticForced = forcedStaticItems };
        return Tuple.Create(mapId, mapStaticData);
    }

    public static List<SpawnpointTemplate> CreateDynamicStaticContainers(Data rawMapDump)
    {
        var data = (
            from li in rawMapDump.LocationLoot.Loot
            where (li.IsContainer ?? false) && (!LootDumpProcessorContext.GetStaticWeaponIds().Contains(li.Items.First().Template))
            select li
        ).ToList();

        foreach (var item in data)
        {
            // remove all but first item from containers items
            item.Items = new List<SptLootItem> { item.Items.First() };
        }

        return data;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="container_counts"></param>
    /// <returns>key = mapid / </returns>
    public static Dictionary<string, Dictionary<string, List<StaticAmmoDetails>>> CreateAmmoDistribution(
        Dictionary<string, List<PreProcessedStaticLoot>> container_counts
    )
    {
        var allMapsAmmoDistro = new Dictionary<string, Dictionary<string, List<StaticAmmoDetails>>>();
        foreach (var mapAndContainers in container_counts)
        {
            var mapid = mapAndContainers.Key;
            var containers = mapAndContainers.Value;

            var ammo = new List<string>();
            foreach (var ci in containers)
            {
                ammo.AddRange(
                    from item in ci.Items
                    where LootDumpProcessorContext.GetTarkovItems().IsBaseClass(item.Template, BaseClasses.Ammo)
                    select (string)item.Template
                );
            }

            var ammo_counts = new List<CaliberTemplateCount>();
            ammo_counts.AddRange(
                ammo.GroupBy(a => a)
                    .Select(g => new CaliberTemplateCount
                    {
                        Caliber = LootDumpProcessorContext.GetTarkovItems().AmmoCaliber(g.Key),
                        Template = g.Key,
                        Count = g.Count(),
                    })
            );
            ammo_counts = ammo_counts.OrderBy(x => x.Caliber).ToList();
            var ammo_distribution = new Dictionary<string, List<StaticAmmoDetails>>();
            foreach (var _tup_3 in ammo_counts.GroupBy(x => x.Caliber))
            {
                var k = _tup_3.Key;
                var g = _tup_3.ToList();
                ammo_distribution[k] = (
                    from gi in g
                    select new StaticAmmoDetails { Tpl = gi.Template, RelativeProbability = gi.Count }
                ).ToList();
            }

            allMapsAmmoDistro.TryAdd(mapid, ammo_distribution);
        }

        return allMapsAmmoDistro;
    }

    /// <summary>
    /// Dict key = map,
    /// value = sub dit:
    ///     key = container Ids
    ///     value = items + counts
    /// </summary>
    public static Dictionary<string, Dictionary<string, StaticLootDetails>> CreateStaticLootDistribution(
        Dictionary<string, List<PreProcessedStaticLoot>> container_counts,
        Dictionary<string, StaticContainerDetails> staticContainers
    )
    {
        var allMapsStaticLootDisto = new Dictionary<string, Dictionary<string, StaticLootDetails>>();
        // Iterate over each map we have containers for
        foreach (var mapContainersKvp in container_counts)
        {
            var mapName = mapContainersKvp.Key;
            var containers = mapContainersKvp.Value;

            var static_loot_distribution = new Dictionary<string, StaticLootDetails>();
            var uniqueContainerTypeIds = Enumerable.Distinct((from ci in containers select ci.Type).ToList());

            foreach (var typeId in uniqueContainerTypeIds)
            {
                var container_counts_selected = (from ci in containers where ci.Type == typeId select ci).ToList();

                // Get array of all times a count of items was found in container
                List<int> itemCountsInContainer = GetCountOfItemsInContainer(container_counts_selected);

                // Create structure to hold item count + weight that it will be picked
                // Group same counts together
                static_loot_distribution[typeId] = new StaticLootDetails();
                static_loot_distribution[typeId].ItemCountDistribution = itemCountsInContainer
                    .GroupBy(i => i)
                    .Select(g => new ItemCountDistribution { Count = g.Key, RelativeProbability = g.Count() })
                    .ToList();

                static_loot_distribution[typeId].ItemDistribution = CreateItemDistribution(container_counts_selected);
            }
            // Key = containers tpl, value = items + count weights
            allMapsStaticLootDisto.TryAdd(mapName, static_loot_distribution);
        }

        return allMapsStaticLootDisto;
    }

    private static List<ItemDistribution> CreateItemDistribution(List<PreProcessedStaticLoot> container_counts_selected)
    {
        // TODO: Change for different algo that splits items per parent once parentid = containerid, then compose
        // TODO: key and finally create distribution based on composed Id instead
        var itemsHitCounts = new Dictionary<MongoId, int>();
        foreach (var ci in container_counts_selected)
        {
            foreach (var cii in ci.Items.Where(cii => cii.ParentId == ci.ContainerId))
            {
                if (itemsHitCounts.ContainsKey(cii.Template))
                    itemsHitCounts[cii.Template] += 1;
                else
                    itemsHitCounts[cii.Template] = 1;
            }
        }

        // WIll create array of objects that have a tpl + relative probability weight value
        return itemsHitCounts.Select(v => new ItemDistribution { Tpl = v.Key, RelativeProbability = v.Value }).ToList();
    }

    private static List<int> GetCountOfItemsInContainer(List<PreProcessedStaticLoot> container_counts_selected)
    {
        var itemCountsInContainer = new List<int>();
        foreach (var containerWithItems in container_counts_selected)
        {
            // Only count item if its parent is the container, only root items are counted (not mod/attachment items)
            itemCountsInContainer.Add(
                (from cii in containerWithItems.Items where cii.ParentId == containerWithItems.ContainerId select cii)
                    .ToList()
                    .Count
            );
        }

        return itemCountsInContainer;
    }
}
