using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Common.Models;
using Common.Models.Input;
using Generator;
using Generator.Helpers;
using Generator.Helpers.Gear;

namespace Common.Bots;

public static class BotParser
{
    private const int ReadAhead = 16;
    private const int QueueCapacity = 64;

    public static async Task<List<GeneratedBot>> Parse(string dumpPath, HashSet<string> botTypes)
    {
        var stopwatch = Stopwatch.StartNew();

        // Build the list of base bot data
        var baseBots = new List<GeneratedBot>();
        foreach (var botType in botTypes)
        {
            var typeToAdd = (BotType)Enum.Parse(typeof(BotType), botType);
            baseBots.Add(new GeneratedBot(typeToAdd));
        }

        var variants = RoleVariants.CreateVariants(baseBots.Select(bot => bot.Role));
        var allBots = baseBots.Concat(variants).ToList();

        DiskHelpers.CreateDirIfDoesntExist(dumpPath);
        var botFiles = Directory.GetFiles(dumpPath, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(botFiles, StringComparer.Ordinal);
        LoggingHelpers.LogToConsole($"{botFiles.Length} bot dump files found");

        var parsedBotIds = new HashSet<string>();
        var totalDupeCount = 0;

        // Loot weights are reduced after every bot and the first copy of a duplicate wins, so the output depends on merge order.
        // Files are read in parallel and dispatched in file order. Each bot type then applies its bots in that order on its own consumer.
        var queues = allBots.ToDictionary(bot => bot.Key, _ => Channel.CreateBounded<List<Datum>>(QueueCapacity));
        var consumers = allBots.Select(bot => Task.Run(() => ConsumeBotsAsync(bot, queues[bot.Key]))).ToList();

        var pending = new Queue<(string FilePath, Task<List<Datum>> Read)>();
        var nextFile = 0;
        while (nextFile < botFiles.Length || pending.Count > 0)
        {
            while (nextFile < botFiles.Length && pending.Count < ReadAhead)
            {
                var filePath = botFiles[nextFile++];
                pending.Enqueue((filePath, Task.Run(() => ReadBotFileAsync(filePath))));
            }

            var (path, read) = pending.Dequeue();
            var botDataList = await read;
            if (botDataList is not null)
            {
                totalDupeCount += await DispatchBotFileAsync(queues, path, botDataList, parsedBotIds);
            }

            if ((nextFile - pending.Count) % 500 == 0)
                Console.WriteLine($"Processing file {nextFile - pending.Count}");
        }

        foreach (var queue in queues.Values)
        {
            queue.Writer.TryComplete();
        }

        await Task.WhenAll(consumers);

        // Handle things we can only do once all data has been processed
        foreach (var bot in allBots)
        {
            if (bot.BotCount == 0)
                continue;

            // Difficulty settings belong to the role, a variant only carries a loadout
            if (bot.VariantDifficulty is null)
            {
                await BaseBotGenerator.AddDifficulties(bot);
            }

            ItemBuildHelpers.SelectBuilds(bot);
            GearChanceHelpers.CalculateModChances(bot);
            GearChanceHelpers.CalculateEquipmentModChances(bot);
            GearChanceHelpers.CalculateEquipmentChances(bot);
            GearChanceHelpers.ApplyModChanceOverrides(bot);
            GearChanceHelpers.ApplyEquipmentChanceOverrides(bot);
            GenerationCountHelpers.ApplyLearnedWeights(bot);
            GearHelpers.ReweightRigs(bot);
            GearHelpers.ReduceAmmoWeightValues(bot);
            GearHelpers.ReduceEquipmentWeightValues(bot.Data.BotInventory.Equipment);
            GearHelpers.ReduceWeightValues(bot.Data.BotAppearance.Voice);
            GearHelpers.ReduceWeightValues(bot.Data.BotAppearance.Feet);
            GearHelpers.ReduceWeightValues(bot.Data.BotAppearance.Body);
            GearHelpers.ReduceWeightValues(bot.Data.BotAppearance.Head);
            GearHelpers.ReduceWeightValues(bot.Data.BotAppearance.Hands);
        }

        foreach (var variant in variants.Where(variant => variant.BotCount > 0))
        {
            RoleVariants.MergeInto(baseBots.First(bot => bot.Role == variant.Role), variant);
        }

        stopwatch.Stop();
        LoggingHelpers.LogToConsole(
            $"{totalDupeCount} dupes were ignored. Took {LoggingHelpers.LogTimeTaken(stopwatch.Elapsed.TotalSeconds)} seconds"
        );

        return baseBots.ToList();
    }

    private static async Task<List<Datum>> ReadBotFileAsync(string filePath)
    {
        Root deSerialisedObject;

        try
        {
            await using var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                useAsync: true
            );

            // Newer captures log the response body on its own; older ones keep the err/data envelope.
            var firstByte = fs.ReadByte();
            while (firstByte >= 0 && char.IsWhiteSpace((char)firstByte))
            {
                firstByte = fs.ReadByte();
            }

            fs.Position = 0;

            deSerialisedObject =
                firstByte == '['
                    ? new Root { data = await JsonSerializer.DeserializeAsync<List<Datum>>(fs) }
                    : await JsonSerializer.DeserializeAsync<Root>(fs);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to parse file from path: {filePath}, skipping. {e.Message}");
            return null;
        }

        if (deSerialisedObject?.data is null)
        {
            Console.WriteLine($"Failed to process file: {filePath} as its data object is null");
            return null;
        }

        return deSerialisedObject.data.ToList();
    }

    private static async Task<int> DispatchBotFileAsync(
        Dictionary<string, Channel<List<Datum>>> queues,
        string filePath,
        List<Datum> botDataList,
        HashSet<string> parsedBotIds
    )
    {
        var dupeCount = 0;
        var botDataByType = new Dictionary<string, List<Datum>>();

        foreach (var botData in botDataList)
        {
            try
            {
                if (!parsedBotIds.Add(botData._id))
                {
                    dupeCount++;
                    continue;
                }

                var role = botData.Info.Settings.Role;
                if (!Enum.TryParse<BotType>(role, true, out var botType))
                {
                    Console.WriteLine($"Skipping: {botData._id} due to unknown role: {role}");
                    continue;
                }

                var key = RoleVariants.Key(botType, botData.Info.Settings.BotDifficulty);
                if (!botDataByType.TryGetValue(key, out var list))
                {
                    list = new List<Datum>();
                    botDataByType[key] = list;
                }
                list.Add(botData);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Bot in file: {filePath} was invalid, skipping - {e.Message}");
            }
        }

        foreach (var kvp in botDataByType)
        {
            if (!queues.TryGetValue(kvp.Key, out var queue))
            {
                Console.WriteLine($"Skipping bot type: {kvp.Key} - not found in base bots");
                continue;
            }

            await queue.Writer.WriteAsync(kvp.Value);
        }

        return dupeCount;
    }

    private static async Task ConsumeBotsAsync(GeneratedBot baseBot, Channel<List<Datum>> queue)
    {
        try
        {
            await foreach (var botDataItems in queue.Reader.ReadAllAsync())
            {
                baseBot.BotCount += botDataItems.Count;

                foreach (var botData in botDataItems)
                {
                    BaseBotGenerator.UpdateBaseDetails(baseBot, botData);
                    BotGearGenerator.AddGear(baseBot, botData);
                    BotLootGenerator.AddLoot(baseBot, botData);
                    BotChancesGenerator.AddChances(baseBot, botData);
                    GenerationCountHelpers.AddCounts(baseBot, botData);
                }
            }
        }
        catch (Exception e)
        {
            // Fail the writer too, otherwise the reader blocks forever on a full queue
            queue.Writer.TryComplete(e);
            throw;
        }
    }
}
