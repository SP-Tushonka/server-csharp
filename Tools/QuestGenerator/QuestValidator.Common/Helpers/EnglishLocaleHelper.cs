using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using QuestValidator.Common.Helpers;

namespace QuestValidator.Common
{
    public static class EnglishLocaleHelper
    {
        private static Dictionary<string, string> _localeCache;

        public static Dictionary<string, string> Locales
        {
            get
            {
                if (_localeCache is null)
                {
                    var localeJson = File.ReadAllText(System.IO.Path.Combine(ServerAssets.Database, "locales", "global", "en.json"));
                    _localeCache = JsonSerializer.Deserialize<Dictionary<string, string>>(localeJson);
                }

                return _localeCache;
            }
        }

        public static string GetQuestNameById(string questId)
        {
            if (Locales.TryGetValue($"{questId} name", out var name))
            {
                return name;
            }

            LoggingHelpers.LogToConsole($"Could not locale key with id {questId}", ConsoleColor.Red);
            return null;
        }
    }
}
