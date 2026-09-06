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

        // Profiles also see trimmed condition lists, so conditions are unioned by content while reward
        // lists follow the newest dump, since those change between game versions.
        private static void FillMissing(JsonObject into, JsonObject from, bool unionArrays = false)
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
                    FillMissing(currentObject, valueObject, unionArrays || key == "conditions");
                }
                else if (current is JsonArray currentArray && value is JsonArray valueArray)
                {
                    FillMissing(currentArray, valueArray, unionArrays);
                }
            }
        }

        // Elements pair up by id, or by position when a dump regenerated the ids and the shapes still match
        private static void FillMissing(JsonArray into, JsonArray from, bool unionArrays)
        {
            if (into.Count == 0)
            {
                foreach (var element in from)
                {
                    into.Add(element?.DeepClone());
                }

                return;
            }

            var signatures = unionArrays ? into.Select(Signature).ToHashSet() : null;
            for (var i = 0; i < from.Count; i++)
            {
                if (from[i] is not JsonObject element)
                {
                    continue;
                }

                var id = element["id"]?.GetValue<string>();
                var match = id is null ? null : into.FirstOrDefault(x => x?["id"]?.GetValue<string>() == id) as JsonObject;
                if (match is null && into.Count == from.Count)
                {
                    match = into[i] as JsonObject;
                }

                if (match is not null)
                {
                    FillMissing(match, element, unionArrays);
                }
                else if (unionArrays && signatures.Add(Signature(element)))
                {
                    into.Add(element.DeepClone());
                }
            }
        }

        // Ids and indexes are regenerated per dump, the rest identifies a condition
        private static string Signature(JsonNode node)
        {
            var copy = node?.DeepClone();
            StripIds(copy);
            return copy?.ToJsonString() ?? "";
        }

        private static void StripIds(JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    obj.Remove("id");
                    obj.Remove("index");
                    foreach (var child in obj.Select(kv => kv.Value).ToList())
                    {
                        StripIds(child);
                    }

                    break;
                case JsonArray arr:
                    foreach (var child in arr)
                    {
                        StripIds(child);
                    }

                    break;
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
