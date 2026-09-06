using System.Text.Json;
using System.Text.Json.Nodes;
using DumpCleaner.Models;
using QuestValidator.Common;
using QuestValidator.Common.Helpers;

public class LocationData
{
    public LocationData(Location location, object rawLocation)
    {
        Location = location;
        RawLocation = rawLocation;
    }

    public Location Location { get; set; }
    public object RawLocation { get; set; }
}

public class LocationParser
{
    private readonly Dictionary<string, LocationData> locations = new Dictionary<string, LocationData>();
    private readonly string outputPath;

    public LocationParser(string outputPath)
    {
        this.outputPath = outputPath;
    }

    internal void AddLocalLootDump(object dumpFile, string rawJson)
    {
        var localStart = JsonSerializer.Deserialize<LocalStart>(dumpFile.ToString());
        var locationData = JsonSerializer.Deserialize<Location>(localStart.locationLoot.ToString());
        var locationName = locationData.Id;

        // already parsed, skip
        if (locations.ContainsKey(locationName))
        {
            return;
        }

        // remove loot as not needed for base.json
        locationData.Loot?.Clear();

        // remove loot from raw file
        JsonNode? parsedJson = JsonNode.Parse(rawJson);

        var locationLoot = parsedJson["locationLoot"];
        locationLoot["Loot"] = new JsonArray();

        var lootFreeJson = locationLoot.ToString();

        locations.Add(locationName, new LocationData(locationData, JsonSerializer.Deserialize<object>(locationLoot)));
    }

    internal void CreateLocationFile()
    {
        foreach (var location in locations)
        {
            if (string.Equals(location.Value.Location.Id, "lighthouse2", StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            JsonWriter.WriteJson(
                location.Value.RawLocation,
                $"{outputPath}/{location.Value.Location.Id.ToLower()}",
                Directory.GetCurrentDirectory(),
                "base"
            );
            LoggingHelpers.LogToConsole($"Found map file: {location.Value.Location.Id} wrote file to output folder");
        }
    }
}
