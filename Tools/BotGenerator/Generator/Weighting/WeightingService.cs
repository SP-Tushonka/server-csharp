using System.Collections.Generic;
using System.Text.Json;
using Common.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using BotType = Common.Models.BotType;

namespace Generator.Weighting
{
    public class Weightings
    {
        public Dictionary<string, Dictionary<string, int>> Equipment { get; set; }

        // Ammo type + (dict of ammo + weight)
        public Dictionary<string, Dictionary<string, int>> Ammo { get; set; }
    }

    public class WeightingService
    {
        private static Dictionary<BotType, Weightings> _weights = null;
        private static Dictionary<string, Dictionary<string, GenerationData>> _generationWeights = null;

        public WeightingService()
        {
            // Cache the loaded  data
            if (_weights != null && _generationWeights != null)
                return;

            var assetsPath = $"{Directory.GetCurrentDirectory()}\\Assets";
            var weightsFilePath = $"{assetsPath}\\weights.json";
            if (!File.Exists(weightsFilePath))
            {
                throw new Exception($"Missing weights.json in /assets ({weightsFilePath})");
            }

            var weightJson = File.ReadAllText(weightsFilePath);
            _weights = JsonSerializer.Deserialize<Dictionary<BotType, Weightings>>(weightJson);

            // bot / itemtype / itemcount
            _generationWeights = ToolJson.Util.DeserializeFromFile<Dictionary<string, Dictionary<string, GenerationData>>>(
                $"{assetsPath}\\generationWeights.json"
            );

            // The server reads an empty whitelist array as null
            foreach (var generationData in _generationWeights.Values.SelectMany(x => x.Values))
            {
                generationData.Whitelist ??= new Dictionary<MongoId, double>();
            }
        }

        public int GetAmmoWeight(string tpl, BotType botType, string caliber)
        {
            if (_weights.ContainsKey(botType))
            {
                var botWeights = _weights[botType];
                if (botWeights.Ammo == null)
                {
                    return 1;
                }

                if (botWeights.Ammo.ContainsKey(caliber))
                {
                    var calibers = botWeights.Ammo[caliber];

                    if (calibers.ContainsKey(tpl))
                    {
                        return calibers[tpl];
                    }
                }
            }

            return 1;
        }

        public int GetItemWeight(string tpl, BotType botType, string slot)
        {
            if (_weights.ContainsKey(botType))
            {
                var botItemList = _weights[botType];

                if (botItemList.Equipment.Keys.Contains(slot, StringComparer.CurrentCultureIgnoreCase))
                {
                    var slotWeights = botItemList.Equipment.FirstOrDefault(x => x.Key.ToLower() == slot).Value;
                    if (slotWeights.Keys.Contains(tpl, StringComparer.CurrentCultureIgnoreCase))
                    {
                        var itemWeight = slotWeights[tpl];
                        return itemWeight;
                    }
                }
            }

            return 1;
        }

        public Dictionary<string, GenerationData> GetBotGenerationWeights(BotType botType)
        {
            _generationWeights.TryGetValue(botType.ToString(), out var result);
            if (result == null)
            {
                return _generationWeights["default"];
            }

            return result;
        }
    }
}
