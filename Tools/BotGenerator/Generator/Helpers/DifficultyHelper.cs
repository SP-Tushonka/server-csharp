using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Common.Models;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace Generator.Helpers
{
    public static class DifficultyHelper
    {
        private static readonly JsonSerializerOptions options = new()
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            Converters = { new JsonStringEnumConverter() },
            AllowTrailingCommas = true,
        };

        private static readonly string[] _difficulties = ["easy", "normal", "hard", "impossible"];

        private static readonly JsonDocumentOptions documentOptions = new() { AllowTrailingCommas = true };

        public static async Task AddDifficultySettings(GeneratedBot botToUpdate, List<string> difficultyFilePaths)
        {
            // Read bot setting files from assets folder that match this bots type
            // Save into dictionary with difficulty as key
            Dictionary<string, DifficultyCategories> difficultySettingsJsons = new();
            var pathsWithBotType = difficultyFilePaths.Where(x =>
                x.Contains($"_{botToUpdate.Role}_BotGlobalSettings", StringComparison.InvariantCultureIgnoreCase)
            );
            foreach (var path in pathsWithBotType)
            {
                var settings = await ReadSettings(path);

                // In PvE raids the client applies the role's PvE file over the regular one, it only lists what differs
                var pvePath = path.Replace("_BotGlobalSettings", "_PvE_BotGlobalSettings");
                if (File.Exists(pvePath))
                {
                    ApplyOverrides(settings, await ReadSettings(pvePath));
                }

                difficultySettingsJsons.Add(GetFileDifficultyFromPath(path), settings.Deserialize<DifficultyCategories>(options));
            }

            foreach (var difficulty in _difficulties)
            {
                var settings = difficultySettingsJsons.FirstOrDefault(x => x.Key.Contains(difficulty));

                // No difficulty settings found, find any settings file and use that
                // This is required for many bot types that only have 'normal' difficulty settings
                if (settings.Key == null)
                {
                    Console.WriteLine($"Difficulty: {difficulty} not found for {botToUpdate.Role}, falling back to any value found");
                    settings = difficultySettingsJsons.FirstOrDefault(x => x.Key != null);
                    if (settings.Key is null)
                    {
                        Console.WriteLine($"No difficulty values found for {botToUpdate.Role}");
                    }
                }

                if (settings.Value != null)
                {
                    botToUpdate.Data.BotDifficulty[difficulty] = settings.Value;
                }
            }
        }

        private static async Task<JsonObject> ReadSettings(string path)
        {
            return JsonNode.Parse(await File.ReadAllTextAsync(path), documentOptions: documentOptions)!.AsObject();
        }

        /// <summary>
        ///     Apply one settings file over another the way the client populates them, objects merge and every other value replaces
        /// </summary>
        /// <param name="settings">Settings to change</param>
        /// <param name="overrides">Values to apply</param>
        private static void ApplyOverrides(JsonObject settings, JsonObject overrides)
        {
            foreach (var (key, value) in overrides)
            {
                if (value is JsonObject child && settings[key] is JsonObject existing)
                {
                    ApplyOverrides(existing, child);
                    continue;
                }

                settings[key] = value?.DeepClone();
            }
        }

        private static string GetFileDifficultyFromPath(string path)
        {
            // Split path into parts and find the last part (filename)
            // Split filename and take the first part (difficulty, easy/normal etc)
            var splitPath = path.Split("\\");
            return splitPath.Last().Split("_")[0];
        }
    }
}
