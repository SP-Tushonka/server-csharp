using System.Collections.Concurrent;
using LootDumpProcessor.Logger;
using LootDumpProcessor.Model.Input;
using LootDumpProcessor.Model.Processing;
using LootDumpProcessor.Serializers.Json;
using LootDumpProcessor.Storage;
using LootDumpProcessor.Storage.Collections;
using LootDumpProcessor.Utils;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LootDumpProcessor.Process.Processor.DumpProcessor;

public class MultithreadSteppedDumpProcessor : IDumpProcessor
{
    private static IJsonSerializer _jsonSerializer = new NetJsonSerializer();

    private static readonly List<Task> Runners = new();

    // if we need to, this variable can be moved to use the factory, but since the factory
    // needs a locking mechanism to prevent dictionary access exceptions, its better to keep
    // a reference to use here
    private static readonly IDataStorage _dataStorage = DataStorageFactory.GetInstance();

    public Dictionary<OutputFileType, object> ProcessDumps(List<PartialData> dumps)
    {
        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("Starting final dump processing", LogLevel.Info);
        var output = new Dictionary<OutputFileType, object>();

        var dumpProcessData = GetDumpProcessData(dumps);
        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("Heavy processing done!", LogLevel.Info);

        var staticContainers = new Dictionary<string, StaticContainerDetails>();
        var staticContainersLock = new object();
        // We need to count how many dumps we have for each map
        var mapDumpCounter = new Dictionary<string, int>();
        var mapDumpCounterLock = new object();
        // dictionary of maps, that has a dictionary of template and hit count
        var mapStaticContainersAggregated = new Dictionary<string, Dictionary<SpawnpointTemplate, int>>();
        var mapStaticContainersAggregatedLock = new object();

        Runners.Clear();
        // BSG changed the map data so static containers are now dynamic, so we need to scan all dumps for the static containers.
        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("Queuing dumps for static data processing", LogLevel.Info);
        foreach (var dumped in dumps)
        {
            Runners.Add(
                Task.Factory.StartNew(() =>
                {
                    if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Debug))
                        LoggerFactory.GetInstance().Log($"Processing static data for file {dumped.BasicInfo.FileName}", LogLevel.Debug);

                    var dataDump = _jsonSerializer.Deserialize<Data>(File.ReadAllText(dumped.BasicInfo.FileName));

                    if (dataDump == null)
                    {
                        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Error))
                            LoggerFactory
                                .GetInstance()
                                .Log($"Failed to deserialize data from file {dumped.BasicInfo.FileName}", LogLevel.Error);
                        return; // Skip processing this dump
                    }

                    var mapId = dataDump.LocationLoot.Id.ToLower();

                    // Because we may use multiple version dumps for map data, merge the static loot between dumps
                    lock (staticContainersLock)
                    {
                        if (!staticContainers.TryGetValue(mapId, out var mapStaticLoot))
                        {
                            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
                                LoggerFactory
                                    .GetInstance()
                                    .Log($"Doing first time process for map {mapId} of real static data", LogLevel.Info);

                            staticContainers[mapId] = new StaticContainerDetails
                            {
                                StaticWeapons = new List<SpawnpointTemplate>(),
                                StaticForced = new List<StaticForced>(),
                            };
                        }
                        else
                        {
                            // .Item1 = map name
                            // .Item2 = force/weapon static arrays
                            var mapStaticContainers = StaticLootProcessor.CreateStaticWeaponsAndStaticForcedContainers(dataDump);

                            var newStaticWeapons = mapStaticContainers
                                .Item2.StaticWeapons.Where(x => mapStaticLoot.StaticWeapons.All(y => y.Id != x.Id))
                                .ToList();
                            var newStaticForced = mapStaticContainers
                                .Item2.StaticForced.Where(x => mapStaticLoot.StaticForced.All(y => y.ContainerId != x.ContainerId))
                                .ToList();

                            mapStaticLoot.StaticWeapons = mapStaticLoot.StaticWeapons.Concat(newStaticWeapons).ToList();
                            mapStaticLoot.StaticForced = mapStaticLoot.StaticForced.Concat(newStaticForced).ToList();
                        }
                    }

                    // Takes care of finding how many "dynamic static containers" we have on the map
                    Dictionary<SpawnpointTemplate, int> mapAggregatedDataDict;
                    lock (mapStaticContainersAggregatedLock)
                    {
                        // Init dict if map key doesnt exist
                        if (!mapStaticContainersAggregated.TryGetValue(mapId, out mapAggregatedDataDict))
                        {
                            mapAggregatedDataDict = new Dictionary<SpawnpointTemplate, int>(SpawnpointTemplateIdComparer.Instance);
                            mapStaticContainersAggregated.Add(mapId, mapAggregatedDataDict);
                        }
                    }

                    // Only process the dump file if the date is higher (after) the configuration date
                    if (!DumpWasMadeAfterConfigThresholdDate(dumped))
                    {
                        return;
                    }

                    // Keep track of how many dumps we have for each map
                    lock (mapDumpCounterLock)
                    {
                        IncrementMapCounterDictionaryValue(mapDumpCounter, mapId);
                    }

                    var containerIgnoreListExists = LootDumpProcessorContext
                        .GetConfig()
                        .ContainerIgnoreList.TryGetValue(mapId, out string[]? ignoreListForMap);
                    foreach (var dynamicStaticContainer in StaticLootProcessor.CreateDynamicStaticContainers(dataDump))
                    {
                        lock (mapStaticContainersAggregatedLock)
                        {
                            if (containerIgnoreListExists && ignoreListForMap.Contains(dynamicStaticContainer.Id))
                            {
                                // Skip adding containers to aggregated data if container id is in ignore list
                                continue;
                            }

                            // Increment times container seen in dump by 1
                            if (!mapAggregatedDataDict.TryAdd(dynamicStaticContainer, 1))
                            {
                                mapAggregatedDataDict[dynamicStaticContainer] += 1;
                            }
                        }
                    }

                    GCHandler.Collect();
                })
            );
        }

        Task.WaitAll(Runners.ToArray());
        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("All static data processing threads finished", LogLevel.Info);
        // Aggregate and calculate the probability of a static container
        mapStaticContainersAggregated
            .ToDictionary(
                kv => kv.Key,
                kv =>
                    kv.Value.Select(td => new StaticContainerData
                        {
                            Template = td.Key,
                            Probability = (float)GetStaticContainerProbability(kv.Key, td, mapDumpCounter), // kv.Key = map name
                        })
                        .ToList()
            )
            .ToList()
            .ForEach(kv => staticContainers[kv.Key].StaticContainers = kv.Value); // Hydrate staticContainers.StaticContainers

        // Static containers
        output.Add(OutputFileType.StaticContainer, staticContainers);
        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("Processing ammo distribution", LogLevel.Info);
        // Ammo distribution
        output.Add(OutputFileType.StaticAmmo, StaticLootProcessor.CreateAmmoDistribution(dumpProcessData.ContainerCounts));

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("Processing static loot distribution", LogLevel.Info);
        // Static loot distribution
        output.Add(
            OutputFileType.StaticLoot,
            StaticLootProcessor.CreateStaticLootDistribution(dumpProcessData.ContainerCounts, staticContainers)
        );

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("Processing loose loot distribution", LogLevel.Info);
        // Loose loot distribution
        var looseLootDistribution = LooseLootProcessor.CreateLooseLootDistribution(
            dumpProcessData.MapCounts,
            dumpProcessData.LooseLootCounts
        );

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("Collecting loose loot distribution information", LogLevel.Info);
        var loot = dumpProcessData.MapCounts.Select(mapCount => mapCount.Key).ToDictionary(mi => mi, mi => looseLootDistribution[mi]);

        output.Add(OutputFileType.LooseLoot, loot);
        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            LoggerFactory.GetInstance().Log("Dump processing fully completed!", LogLevel.Info);
        return output;
    }

    private static bool DumpWasMadeAfterConfigThresholdDate(PartialData dataDump)
    {
        return FileDateParser.TryParseFileDate(dataDump.BasicInfo.FileName, out var fileDate)
            && fileDate.HasValue
            && fileDate.Value > LootDumpProcessorContext.GetConfig().DumpProcessorConfig.SpawnContainerChanceIncludeAfterDate;
    }

    private static void IncrementMapCounterDictionaryValue(Dictionary<string, int> mapDumpCounter, string mapName)
    {
        if (!mapDumpCounter.TryAdd(mapName, 1))
        {
            // Dict has map, increment count by 1
            mapDumpCounter[mapName] += 1;
        }
    }

    private static double GetStaticContainerProbability(
        string mapName,
        KeyValuePair<SpawnpointTemplate, int> td,
        Dictionary<string, int> mapDumpCounter
    )
    {
        return Math.Round((double)((decimal)td.Value / (decimal)mapDumpCounter[mapName]), 2);
    }

    private static DumpProcessData GetDumpProcessData(List<PartialData> dumps)
    {
        var dumpProcessData = new DumpProcessData();

        dumps
            .GroupBy(dump => dump.BasicInfo.Map)
            .ToList()
            .ForEach(tuple =>
            {
                var mapName = tuple.Key;
                var partialFileMetaData = tuple.ToList();
                if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
                    LoggerFactory
                        .GetInstance()
                        .Log($"Processing map {mapName}, total dump data to process: {partialFileMetaData.Count}", LogLevel.Info);
                dumpProcessData.MapCounts[mapName] = partialFileMetaData.Count;

                var lockObjectContainerCounts = new object();

                var lockObjectCounts = new object();
                var looseLootCounts = new LooseLootCounts();

                var lockObjectDictionaryCounts = new object();
                var dictionaryCounts = new FlatKeyableDictionary<string, int>();
                looseLootCounts.Counts = dictionaryCounts.GetKey();

                /*
                var dictionaryItemCounts = new FlatKeyableDictionary<string, List<string>>();
                counts.Items = dictionaryItemCounts.GetKey();
                */

                var lockObjectDictionaryItemProperties = new object();
                var dictionaryItemProperties = new FlatKeyableDictionary<string, FlatKeyableList<SpawnpointTemplate>>();

                var actualDictionaryItemProperties = new FlatKeyableDictionary<string, IKey>();
                looseLootCounts.ItemProperties = actualDictionaryItemProperties.GetKey();

                dumpProcessData.LooseLootCounts.Add(mapName, looseLootCounts.GetKey());

                BlockingCollection<PartialData> _partialDataToProcess = new();

                // add the items to the queue
                foreach (var partialData in partialFileMetaData)
                {
                    _partialDataToProcess.Add(partialData);
                }

                // Call GC before running threads
                partialFileMetaData = null;
                tuple = null;
                GCHandler.Collect();

                // The data storage factory has a lock, we dont want the locks to occur when multithreading
                for (int i = 0; i < LootDumpProcessorContext.GetConfig().Threads; i++)
                {
                    Runners.Add(
                        Task.Factory.StartNew(
                            () =>
                            {
                                while (_partialDataToProcess.TryTake(out var partialData, TimeSpan.FromMilliseconds(5000)))
                                {
                                    try
                                    {
                                        var dumpData = _dataStorage.GetItem<ParsedDump>(partialData.ParsedDumpKey);

                                        // Static containers
                                        lock (lockObjectContainerCounts)
                                        {
                                            if (!dumpProcessData.ContainerCounts.ContainsKey(mapName))
                                            {
                                                dumpProcessData.ContainerCounts.Add(mapName, dumpData.Containers);
                                            }
                                            else
                                            {
                                                dumpProcessData.ContainerCounts[mapName].AddRange(dumpData.Containers);
                                            }
                                        }

                                        // Loose loot into ids on files
                                        var loadedDictionary = _dataStorage.GetItem<
                                            SubdivisionedKeyableDictionary<string, List<SpawnpointTemplate>>
                                        >(dumpData.LooseLoot.ItemProperties);
                                        foreach (var (uniqueKey, containerTemplate) in loadedDictionary)
                                        {
                                            var count = dumpData.LooseLoot.Counts[uniqueKey];
                                            lock (lockObjectDictionaryCounts)
                                            {
                                                if (dictionaryCounts.ContainsKey(uniqueKey))
                                                    dictionaryCounts[uniqueKey] += count;
                                                else
                                                    dictionaryCounts[uniqueKey] = count;
                                            }

                                            /*
                                            var itemList = dumpData.LooseLoot.Items[k];
                                            if (!dictionaryItemCounts.TryGetValue(k, out var itemCounts))
                                            {
                                                itemCounts = new List<string>();
                                                dictionaryItemCounts.Add(k, itemCounts);
                                            }
                                            itemCounts.AddRange(itemList);
                                            */

                                            lock (lockObjectDictionaryItemProperties)
                                            {
                                                if (!dictionaryItemProperties.TryGetValue(uniqueKey, out var values))
                                                {
                                                    values = new FlatKeyableList<SpawnpointTemplate>();
                                                    dictionaryItemProperties.Add(uniqueKey, values);
                                                    actualDictionaryItemProperties.Add(uniqueKey, values.GetKey());
                                                }

                                                values.AddRange(containerTemplate);
                                            }
                                        }

                                        lock (lockObjectCounts)
                                        {
                                            looseLootCounts.MapSpawnpointCount.Add(dumpData.LooseLoot.MapSpawnpointCount);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Error))
                                            LoggerFactory.GetInstance().Log($"ERROR OCCURRED:{e.Message}\n{e.StackTrace}", LogLevel.Error);
                                    }
                                }
                            },
                            TaskCreationOptions.LongRunning
                        )
                    );
                }

                // Wait until all runners are done processing
                while (!Runners.All(r => r.IsCompleted))
                {
                    if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
                        LoggerFactory
                            .GetInstance()
                            .Log(
                                $"One or more file processors are still processing files. Waiting {LootDumpProcessorContext.GetConfig().ThreadPoolingTimeoutMs}ms before checking again",
                                LogLevel.Info
                            );
                    Thread.Sleep(TimeSpan.FromMilliseconds(LootDumpProcessorContext.GetConfig().ThreadPoolingTimeoutMs));
                }

                foreach (var (_, value) in dictionaryItemProperties)
                {
                    _dataStorage.Store(value);
                }

                _dataStorage.Store(dictionaryCounts);
                dictionaryCounts = null;
                GCHandler.Collect();
                /*
                DataStorageFactory.GetInstance().Store(dictionaryItemCounts);
                dictionaryItemCounts = null;
                GC.Collect();
                */
                _dataStorage.Store(actualDictionaryItemProperties);
                actualDictionaryItemProperties = null;
                GCHandler.Collect();
                _dataStorage.Store(looseLootCounts);
                looseLootCounts = null;
                GCHandler.Collect();
            });
        return dumpProcessData;
    }
}
