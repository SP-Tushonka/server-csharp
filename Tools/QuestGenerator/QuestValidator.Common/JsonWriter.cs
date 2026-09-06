using System;
using System.IO;
using QuestValidator.Helpers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace QuestValidator.Common
{
    public static class JsonWriter
    {
        private static JsonUtil _jsonUtil = new JsonUtil([new SptJsonConverterRegistrator()]);

        public static void WriteJson<T>(T itemToSerialise, string outputFolderName, string workingPath, string fileName)
        {
            var outputPath = $"{workingPath}\\output\\{outputFolderName}";
            DiskHelpers.CreateDirIfDoesntExist(outputPath);

            var json = _jsonUtil.Serialize(itemToSerialise, true);
            File.WriteAllText($"{outputPath}\\{fileName}.json", json);

            Console.WriteLine($"wrote {fileName}.json file to {outputPath}");
        }
    }
}
