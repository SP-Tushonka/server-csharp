using System.Text.Json;
using System.Text.Json.Nodes;
using AssortGenerator.Common.Helpers;
using DumpCleaner;
using QuestValidator.Common;
using QuestValidator.Common.Helpers;
using QuestValidator.Helpers;

var inputPath = DiskHelpers.CreateWorkingFolders();
InputFileHelper.SetInputFiles(inputPath);
var locationParser = new LocationParser("locations");

foreach (var path in InputFileHelper.GetInputFilePaths())
{
    var filename = Path.GetFileNameWithoutExtension(path);
    var names = DumpFiles.filenames.Find(x => filename.StartsWith(x.InputName));

    if (names == null)
    {
        LoggingHelpers.LogToConsole($"No mapping found for file: {filename} Skipping", ConsoleColor.Yellow);
        continue;
    }

    var rawJson = File.ReadAllText(path);
    object dumpFile;
    try
    {
        JsonObject dump = JsonSerializer.Deserialize<JsonObject>(rawJson);

        //Handle the old dumper
        if (dump["err"] is not null)
        {
            dumpFile = dump["data"];
            rawJson = JsonSerializer.Serialize(dumpFile);
        }
        else
        {
            dumpFile = dump;
        }
    }
    catch (Exception e)
    {
        LoggingHelpers.LogError($"Failed to parse: {path}");
        Console.WriteLine(e);
        throw;
    }

    if (dumpFile == null)
    {
        LoggingHelpers.LogWarning($"file: {filename} had no data in it, skipping");
        continue;
    }

    // Special case, Do special tasks with it
    if (names.SpecialCase)
    {
        HandleSpecialCase(names, dumpFile, rawJson);
    }
    else
    {
        JsonWriter.WriteJson(dumpFile, names.OutputFolder, Directory.GetCurrentDirectory(), names.OutputName);
        LoggingHelpers.LogToConsole($"Found file: {filename} wrote file to output folder");
    }
}

locationParser.CreateLocationFile();
return;

void HandleSpecialCase(DumpData names, object dumpFile, string rawJson)
{
    switch (names.InputName)
    {
        case "resp.client.locations":
            //HandleLocationsFile(names, dumpFile);
            return;
        case "resp.client.match.local.start":
            locationParser.AddLocalLootDump(dumpFile, rawJson);

            return;
        case "resp.client.trading.api.traderSettings":
            HandleTraderSettingsFile(names, dumpFile);

            return;
        case "resp.client.achievement.list":
            HandleAchievementFile(names, dumpFile);
            break;
    }
}

void HandleTraderSettingsFile(DumpData names, object dumpFile)
{
    var traders = JsonSerializer.Deserialize<List<object>>(dumpFile.ToString());

    foreach (var trader in traders)
    {
        var traderData = JsonSerializer.Deserialize<Trader>(trader.ToString());
        //traderData.sell_category = TraderSellCategories.GetCategoriesByTraderId(traderData._id);

        JsonWriter.WriteJson(trader, $"{names.OutputFolder}/{traderData._id}", Directory.GetCurrentDirectory(), "base");
        LoggingHelpers.LogToConsole($"Found trader file: {traderData._id} wrote file to output folder");
    }
}

void HandleAchievementFile(DumpData names, object dumpFile)
{
    var achievements = JsonSerializer.Deserialize<Achievements>(dumpFile.ToString());
    JsonWriter.WriteJson(achievements.elements, $"{names.OutputFolder}", Directory.GetCurrentDirectory(), "achievements");
}

public class Achievements
{
    public required List<object> elements { get; set; }
}
