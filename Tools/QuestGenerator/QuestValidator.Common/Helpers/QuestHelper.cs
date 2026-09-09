using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AssortGenerator.Common.Helpers;
using QuestValidator.Models.Other;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;

namespace QuestValidator.Common.Helpers
{
    public static class QuestHelper
    {
        // Quest to lookup is key, previous quest is PreceedingQuest value

        private static List<List<Quest>> _liveQuestData;
        private static Dictionary<MongoId, Quest> _questData;
        private static HashSet<string> _questBlacklistData;
        private static JsonUtil _jsonUtil;

        static QuestHelper()
        {
            _jsonUtil = DI.GetInstance().GetService<JsonUtil>();
        }

        public static Dictionary<MongoId, Quest> GetQuestData(string filename = "quests")
        {
            if (_questData is not null)
            {
                return _questData;
            }

            var questFilePath = InputFileHelper.GetInputFilePaths().FirstOrDefault(x => x.Contains(filename));
            if (questFilePath is null)
            {
                return null;
            }

            var questDataJson = File.ReadAllText(questFilePath);
            _questData = _jsonUtil.Deserialize<Dictionary<MongoId, Quest>>(questDataJson);

            return _questData;
        }

        // Every dump is one profile's view and omits fields that profile has no use for, so the copies of
        // a quest are merged. The newest dump leads, since quests change between game versions, and the
        // older copies only fill its gaps.
        public static List<Quest> LoadMergedLiveQuests(HashSet<string> questBlacklist)
        {
            var merged = new Dictionary<string, JsonObject>();
            var paths = InputFileHelper.GetInputFilePaths().Where(x => x.Contains("resp.client.quest.list_")).OrderBy(DumpTimestamp);
            foreach (var path in paths)
            {
                if (JsonNode.Parse(File.ReadAllText(path)) is not JsonArray quests)
                {
                    continue;
                }

                foreach (var node in quests)
                {
                    if (node is not JsonObject quest)
                    {
                        continue;
                    }

                    var id = quest["_id"]?.GetValue<string>();
                    if (id is null || (questBlacklist?.Contains(id) ?? false))
                    {
                        continue;
                    }

                    var copy = (JsonObject)quest.DeepClone();
                    if (merged.TryGetValue(id, out var older))
                    {
                        FillMissing(copy, older);
                    }

                    merged[id] = copy;
                }
            }

            return merged.Values.Select(quest => _jsonUtil.Deserialize<Quest>(quest.ToJsonString())).ToList();
        }

        private static long DumpTimestamp(string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            return long.TryParse(name[(name.LastIndexOf('_') + 1)..], out var stamp) ? stamp : 0;
        }

        // The newest dump wins outright. Conditions, rewards and localization are reworked between
        // versions, so merging their arrays across dumps would keep gates live has removed. Older dumps
        // only supply keys a newer dump lacks.
        private static void FillMissing(JsonObject into, JsonObject from)
        {
            foreach (var (key, value) in from)
            {
                if (value is null)
                {
                    continue;
                }

                var current = into[key];
                if (current is null)
                {
                    into[key] = value.DeepClone();
                }
                else if (current is JsonObject currentObject && value is JsonObject valueObject)
                {
                    FillMissing(currentObject, valueObject);
                }
            }
        }

        public static List<List<Quest>> GetLiveQuestData(string filename = "resp.client.quest.list_")
        {
            if (_liveQuestData is not null)
            {
                return _liveQuestData;
            }

            _liveQuestData = [];
            var questFilePaths = InputFileHelper.GetInputFilePaths().Where(x => x.Contains(filename)).ToList();
            if (!questFilePaths.Any())
            {
                return null;
            }

            foreach (var questDataJson in questFilePaths.Select(File.ReadAllText))
            {
                _liveQuestData.Add(_jsonUtil.Deserialize<List<Quest>>(questDataJson));
            }

            return _liveQuestData;
        }

        public static string GetQuestNameById(string id)
        {
            return QuestNames.GetNameById(id);
        }

        public static bool DoesIconExist(string iconPath, string imageId)
        {
            var filesInPath = Directory.GetFiles(iconPath);
            return filesInPath.Any(x => x.Contains(imageId));
        }

        public static HashSet<string> GetQuestBlacklist()
        {
            if (_questBlacklistData is null)
            {
                var questBlacklistPath = InputFileHelper.GetInputFilePaths().FirstOrDefault(x => x.Contains("blacklist"));
                if (questBlacklistPath is null)
                {
                    return null;
                }

                var questBlacklistJson = File.ReadAllText(questBlacklistPath);
                _questBlacklistData = _jsonUtil.Deserialize<HashSet<string>>(questBlacklistJson);
            }

            return _questBlacklistData;
        }
    }
}
