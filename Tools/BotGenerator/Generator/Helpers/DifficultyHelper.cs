using System.Text.Json;
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
                await using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

                var serialisedDifficultySettings = await JsonSerializer.DeserializeAsync<DifficultyCategories>(fs, options);

                var difficultyOfFile = GetFileDifficultyFromPath(path);
                difficultySettingsJsons.Add(difficultyOfFile, serialisedDifficultySettings);
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

        private static string GetFileDifficultyFromPath(string path)
        {
            // Split path into parts and find the last part (filename)
            // Split filename and take the first part (difficulty, easy/normal etc)
            var splitPath = path.Split("\\");
            return splitPath.Last().Split("_")[0];
        }
    }
}
