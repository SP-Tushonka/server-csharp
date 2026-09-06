using System.Collections.Generic;
using System.IO;
using Common.Json;
using Common.Models;

namespace Common
{
    public class JsonWriter
    {
        private readonly string _workingPath;
        private readonly string _outputFolderName;

        public JsonWriter(string workingPath, string outputFolderName)
        {
            _workingPath = workingPath;
            _outputFolderName = outputFolderName;
        }

        public void WriteJson(List<GeneratedBot> bots)
        {
            var outputPath = $"{_workingPath}\\{_outputFolderName}";
            DiskHelpers.CreateDirIfDoesntExist(outputPath);

            foreach (var bot in bots)
            {
                if (bot.Data.BotAppearance.Body.Count == 0) // only process files that have data in them, no body = no dumps
                {
                    LoggingHelpers.LogToConsole($"Unable to process bot type: {bot.Role}, skipping", ConsoleColor.DarkRed);
                    continue;
                }
                var output = ToolJson.Util.Serialize(bot.Data, true);
                Console.WriteLine($"Writing json file {bot.Role} to {outputPath}");
                File.WriteAllText($"{outputPath}\\{bot.Role.ToString().ToLower()}.json", output);
                Console.WriteLine($"file {bot.Role} written to {outputPath}");
            }
        }
    }
}
