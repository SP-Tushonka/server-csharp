using LootDumpProcessor.Logger;
using LootDumpProcessor.Model.Processing;
using LootDumpProcessor.Storage;
using LootDumpProcessor.Storage.Collections;
using LootDumpProcessor.Utils;
using NumSharp;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LootDumpProcessor.Process.Processor;

public static class LooseLootProcessor
{
    public static PreProcessedLooseLoot PreProcessLooseLoot(List<SpawnpointTemplate> looseloot)
    {
        var looseloot_ci = new PreProcessedLooseLoot { Counts = new Dictionary<string, int>() };
        var temporalItemProperties = new SubdivisionedKeyableDictionary<string, List<SpawnpointTemplate>>();
        looseloot_ci.ItemProperties = (AbstractKey)temporalItemProperties.GetKey();
        looseloot_ci.MapSpawnpointCount = looseloot.Count;
        var uniqueIds = new Dictionary<string, object>();
        // sometimes the rotation changes very slightly in the dumps for the same location / rotation spawnpoint
        // use rounding to make sure it is not generated to two spawnpoint

        foreach (var looseLootTemplate in looseloot)
        {
            // the bsg ids are insane.
            // Sometimes the last 7 digits vary but they spawn the same item at the same position
            // e.g. for the quest item "60a3b65c27adf161da7b6e14" at "loot_bunker_quest (3)555192"
            // so the first approach was to remove the last digits.
            // We then saw, that sometimes when the last digits differ for the same string, also the position
            // differs.
            // We decided to group over the position/rotation/useGravity since they make out a distinct spot
            var saneId = looseLootTemplate.GetSaneId();
            if (!uniqueIds.ContainsKey(saneId))
            {
                uniqueIds[saneId] = looseLootTemplate.Id;
                if (looseloot_ci.Counts.ContainsKey(saneId))
                    looseloot_ci.Counts[saneId]++;
                else
                    looseloot_ci.Counts[saneId] = 1;
            }

            if (!temporalItemProperties.TryGetValue(saneId, out var templates))
            {
                templates = new FlatKeyableList<SpawnpointTemplate>();
                temporalItemProperties.Add(saneId, templates);
            }

            templates.Add(looseLootTemplate);
        }

        DataStorageFactory.GetInstance().Store(temporalItemProperties);
        return looseloot_ci;
    }

    public static Dictionary<string, LooseLoot> CreateLooseLootDistribution(
        Dictionary<string, int> map_counts,
        Dictionary<string, IKey> looseloot_counts
    )
    {
        var forcedConfidence = LootDumpProcessorContext.GetConfig().ProcessorConfig.SpawnPointToleranceForForced / 100;
        var probabilities = new Dictionary<string, Dictionary<string, double>>();
        var looseLootDistribution = new Dictionary<string, LooseLoot>();
        foreach (var _tup_1 in map_counts)
        {
            var mapName = _tup_1.Key;
            var mapCount = _tup_1.Value;
            probabilities[mapName] = new Dictionary<string, double>();

            // A map key can exist in forced_loose.yaml with no items (parsed as null), or be missing entirely.
            // Treat either case as an empty set so downstream lookups don't throw a NullReferenceException.
            if (
                !LootDumpProcessorContext.GetForcedLooseItems().TryGetValue(mapName, out var forcedLooseForMap)
                || forcedLooseForMap == null
            )
            {
                if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Warning))
                    LoggerFactory
                        .GetInstance()
                        .Log(
                            $"Map: {mapName} has no forced loose loot items defined in forced_loose.yaml, treating as empty",
                            LogLevel.Warning
                        );
                forcedLooseForMap = new HashSet<string>();
            }
            var looseLootCounts = DataStorageFactory.GetInstance().GetItem<LooseLootCounts>(looseloot_counts[mapName]);

            var counts = DataStorageFactory.GetInstance().GetItem<FlatKeyableDictionary<string, int>>(looseLootCounts.Counts);
            foreach (var (idi, cnt) in counts)
            {
                probabilities[mapName][idi] = (double)((decimal)cnt / mapCount);
            }

            // No longer used, dispose
            counts = null;

            // we want to clean up the data, so we calculate the mean for the values we get raw
            // For whatever reason, we sometimes get dumps that have A LOT more loose loot point than
            // the average
            var values = looseLootCounts.MapSpawnpointCount.Select(Convert.ToDouble);
            var initialMean = np.mean(np.array(values)).ToArray<double>().First();
            var looseLootCountTolerancePercentage =
                LootDumpProcessorContext.GetConfig().ProcessorConfig.LooseLootCountTolerancePercentage / 100;
            // We calculate here a high point to check, anything above this value will be ignored
            // The data that was inside those loose loot points still counts for them though!
            var high = initialMean * (1 + looseLootCountTolerancePercentage);
            looseLootCounts.MapSpawnpointCount = looseLootCounts.MapSpawnpointCount.Where(v => v <= high).ToList();

            var spawnpointsForced = new List<Spawnpoint>();
            var spawnpoints = new List<Spawnpoint>();
            looseLootDistribution[mapName] = new LooseLoot
            {
                SpawnpointCount = new SpawnpointCount
                {
                    Mean = np.mean(np.array(looseLootCounts.MapSpawnpointCount)),
                    Std = np.std(np.array(looseLootCounts.MapSpawnpointCount)),
                },
                SpawnpointsForced = spawnpointsForced,
                Spawnpoints = spawnpoints,
            };

            var itemProperties = DataStorageFactory
                .GetInstance()
                .GetItem<FlatKeyableDictionary<string, IKey>>(looseLootCounts.ItemProperties);
            foreach (var (spawnPoint, itemList) in itemProperties)
            {
                var itemsCounts = new Dictionary<ComposedKey, int>();

                var savedItemProperties = DataStorageFactory.GetInstance().GetItem<FlatKeyableList<SpawnpointTemplate>>(itemList);
                foreach (var savedTemplateProperties in savedItemProperties)
                {
                    var key = ComposedKeys.Create(savedTemplateProperties.Items);
                    if (itemsCounts.ContainsKey(key))
                        itemsCounts[key] += 1;
                    else
                        itemsCounts[key] = 1;
                }

                // Group by arguments to create possible positions / rotations per spawnpoint

                // check if grouping is unique
                var itemListSorted = savedItemProperties
                    .Select(template => (template.GetSaneId(), template))
                    .GroupBy(g => g.Item1)
                    .ToList();

                if (itemListSorted.Count > 1)
                {
                    throw new Exception("More than one saneKey found");
                }

                var spawnPoints = itemListSorted.First().Select(v => v.template).ToList();
                var locationId = spawnPoints[0].GetLocationId();
                var template = spawnPoints[0].DeepClone();
                var firstItemTpl = template.Items?.FirstOrDefault()?.Template.ToString();
                //template.Root = null; // Why do we do this, not null in bsg data
                var itemDistribution = itemsCounts
                    .Select(kv => new LooseLootItemDistribution { ComposedKey = kv.Key, RelativeProbability = kv.Value })
                    .ToList();

                // If any of the items is a quest item or forced loose loot items, or the item normally appreas 99.5%
                // Only add position to forced loot if it has only 1 item in the array.
                if (
                    itemDistribution.Count == 1
                    && firstItemTpl != null
                    && (LootDumpProcessorContext.GetTarkovItems().IsQuestItem(firstItemTpl) || forcedLooseForMap.Contains(firstItemTpl))
                )
                {
                    var spawnPointToAdd = new Spawnpoint
                    {
                        LocationId = locationId,
                        Probability = probabilities[mapName][spawnPoint],
                        Template = template,
                    };
                    spawnpointsForced.Add(spawnPointToAdd);
                }
                else if (probabilities[mapName][spawnPoint] > forcedConfidence)
                {
                    var spawnPointToAdd = new Spawnpoint
                    {
                        LocationId = locationId,
                        Probability = probabilities[mapName][spawnPoint],
                        Template = template,
                    };
                    spawnpointsForced.Add(spawnPointToAdd);
                    if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Warning))
                        LoggerFactory
                            .GetInstance()
                            .Log(
                                $"Item: {template.Id} has > {LootDumpProcessorContext.GetConfig().ProcessorConfig.SpawnPointToleranceForForced}% spawn chance in spawn point: {spawnPointToAdd.LocationId} but isn't in forced loot, adding to forced",
                                LogLevel.Warning
                            );
                }
                else // Normal spawn point, add to non-forced spawnpoint array
                {
                    var spawnPointToAdd = new Spawnpoint
                    {
                        LocationId = locationId,
                        Probability = probabilities[mapName][spawnPoint],
                        Template = template,
                        ItemDistribution = itemDistribution,
                    };

                    var templateItems = new List<SptLootItem>();
                    template.Items = templateItems;

                    var group = spawnPoints
                        .GroupBy(template => ComposedKeys.Create(template.Items))
                        .ToDictionary(g => g.Key, g => g.ToList());
                    foreach (var distribution in itemDistribution)
                    {
                        if (group.TryGetValue(distribution.ComposedKey, out var items))
                        {
                            var itemDistributionItemList = items.First().Items;
                            // Find the item with no parent id, this is essentially the "Root" of the actual item
                            var firstItemInTemplate = itemDistributionItemList.FirstOrDefault(i => string.IsNullOrEmpty(i.ParentId));
                            firstItemInTemplate.ComposedKey = distribution.ComposedKey.Key;

                            templateItems.AddRange(itemDistributionItemList);
                        }
                        else
                        {
                            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Error))
                                LoggerFactory
                                    .GetInstance()
                                    .Log(
                                        $"Composed key {distribution.ComposedKey?.Key} was on loose loot distribution for spawn point {template.Id} but the spawn points didnt contain a template matching it.",
                                        LogLevel.Error
                                    );
                        }
                    }

                    spawnpoints.Add(spawnPointToAdd);
                }
            }

            // # Test for duplicate position
            // # we removed most of them by "rounding away" the jitter in rotation,
            // # there are still a few duplicate locations with distinct difference in rotation left though
            // group_fun = lambda x: (
            //     x["template"]["Position"]["x"],
            //     x["template"]["Position"]["y"],
            //     x["template"]["Position"]["z"],
            // )
            // test = sorted(loose_loot_distribution[mi]["spawnpoints"], key=group_fun)
            // test_grouped = groupby(test, group_fun)
            // test_len = []
            // for k, g in test_grouped:
            //     gl = list(g)
            //     test_len.append(len(gl))
            //     if len(gl) > 1:
            //         print(gl)
            //
            // print(mi, np.unique(test_len, return_counts=True))
            looseLootDistribution[mapName].Spawnpoints = spawnpoints.OrderBy(x => x.Template.Id).ToList();
            // Cross check with forced loot in dumps vs items defined in forced_loose.yaml
            var forcedTplsInConfig = new HashSet<string>((from forceditem in forcedLooseForMap select forceditem).ToList());
            var forcedTplsFound = new HashSet<string>(
                (from forceditem in spawnpointsForced select forceditem.Template.Items.First().Template.ToString()).ToList()
            );

            // All the tpls that are defined in the forced_loose.yaml for this map that are not found as forced
            foreach (var itemTpl in forcedTplsInConfig)
            {
                if (!forcedTplsFound.Contains(itemTpl))
                {
                    if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Error))
                        LoggerFactory
                            .GetInstance()
                            .Log($"Expected item: {itemTpl} defined in forced_loose.yaml config not found in forced loot", LogLevel.Error);
                }
            }

            // All the tpls that are found as forced in output file but not in the forced_loose.yaml config
            foreach (var itemTpl in forcedTplsFound)
            {
                if (!forcedTplsInConfig.Contains(itemTpl))
                {
                    if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Warning))
                        LoggerFactory
                            .GetInstance()
                            .Log(
                                $"Map: {mapName} Item: {itemTpl} not defined in forced_loose.yaml config but was flagged as forced by code",
                                LogLevel.Warning
                            );
                }
            }
        }

        return looseLootDistribution;
    }
}
