using System.Collections.Concurrent;
using Common.Models;
using Common.Models.Input;
using Generator.Weighting;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using BotType = Common.Models.BotType;

namespace Generator.Helpers.Gear
{
    public static class GearChanceHelpers
    {
        // Shared by every bot type consumer. Each inner map belongs to one role.
        private static readonly ConcurrentDictionary<string, Dictionary<string, int>> weaponModCount = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, int>> weaponSlotCount = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, int>> equipmentModCount = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, int>> equipmentSlotCount = new();

        public static void AddModChances(GeneratedBot bot, Datum baseBot)
        {
            // TODO: Further split these counts by equipment slot? (ex. "FirstPrimaryWeapon", "Holster", etc.)
            var validSlots = new List<string> { "FirstPrimaryWeapon", "SecondPrimaryWeapon", "Holster" };

            var modCounts = weaponModCount.GetOrAdd(baseBot.Info.Settings.Role.ToLower(), _ => new Dictionary<string, int>());
            var slotCounts = weaponSlotCount.GetOrAdd(baseBot.Info.Settings.Role.ToLower(), _ => new Dictionary<string, int>());

            CountSlotsAndMods(baseBot, validSlots, modCounts, slotCounts);
        }

        public static void CalculateModChances(GeneratedBot bot)
        {
            var modCounts = weaponModCount.GetOrAdd(bot.Role.ToString(), _ => new Dictionary<string, int>());
            var slotCounts = weaponSlotCount.GetOrAdd(bot.Role.ToString(), _ => new Dictionary<string, int>());

            bot.Data.BotChances.WeaponModsChances = slotCounts.ToDictionary(
                kvp => kvp.Key,
                kvp => (double)GetPercent(kvp.Value, modCounts.GetValueOrDefault(kvp.Key))
            );
        }

        public static void AddEquipmentModChances(GeneratedBot bot, Datum baseBot)
        {
            var modCounts = equipmentModCount.GetOrAdd(baseBot.Info.Settings.Role.ToLower(), _ => new Dictionary<string, int>());
            var slotCounts = equipmentSlotCount.GetOrAdd(baseBot.Info.Settings.Role.ToLower(), _ => new Dictionary<string, int>());

            // TODO: Further split these counts by equipment slot? (ex. "FirstPrimaryWeapon", "Holster", etc.)
            var validSlots = new List<string> { "Headwear", "ArmorVest", "TacticalVest" };

            CountSlotsAndMods(baseBot, validSlots, modCounts, slotCounts);
        }

        private static void CountSlotsAndMods(
            Datum baseBot,
            List<string> validSlots,
            Dictionary<string, int> modCounts,
            Dictionary<string, int> slotCounts
        )
        {
            var validParents = new List<string>();

            foreach (var inventoryItem in baseBot.Inventory.items)
            {
                if (validSlots.Contains(inventoryItem.slotId))
                {
                    validParents.Add(inventoryItem._id);
                }
                else if (validParents.Contains(inventoryItem.parentId))
                {
                    validParents.Add(inventoryItem._id);
                }
                else
                {
                    continue;
                }

                var template = ItemTemplateHelper.GetTemplateById(inventoryItem._tpl);
                var parentTemplate = ItemTemplateHelper.GetTemplateById(
                    baseBot.Inventory.items.Single(i => i._id == inventoryItem.parentId)._tpl
                );

                if (!(parentTemplate?.Properties?.Slots?.FirstOrDefault(slot => slot.Name == inventoryItem.slotId)?.Required ?? false))
                {
                    if (modCounts.ContainsKey(inventoryItem.slotId.ToLower()))
                    {
                        modCounts[inventoryItem.slotId.ToLower()]++;
                    }
                    else
                    {
                        modCounts.Add(inventoryItem.slotId.ToLower(), 1);
                    }
                }

                if (template?.Properties?.Slots?.Any() != true)
                {
                    // Item has no slots, nothing to count here
                    continue;
                }

                foreach (var slot in template.Properties.Slots)
                {
                    if (slot.Required)
                    {
                        continue;
                    }

                    if (slot.Name.StartsWith("camora"))
                    {
                        continue;
                    }

                    if (slotCounts.ContainsKey(slot.Name.ToLower()))
                    {
                        slotCounts[slot.Name.ToLower()]++;
                    }
                    else
                    {
                        slotCounts.Add(slot.Name.ToLower(), 1);
                    }
                }
            }
        }

        public static void CalculateEquipmentModChances(GeneratedBot bot)
        {
            var modCounts = equipmentModCount.GetOrAdd(bot.Role.ToString(), _ => new Dictionary<string, int>());
            var slotCounts = equipmentSlotCount.GetOrAdd(bot.Role.ToString(), _ => new Dictionary<string, int>());

            bot.Data.BotChances.EquipmentModsChances = slotCounts.ToDictionary(
                kvp => kvp.Key,
                kvp => (double)GetPercent(kvp.Value, modCounts.GetValueOrDefault(kvp.Key))
            );
        }

        internal static void ApplyEquipmentChanceOverrides(GeneratedBot botToUpdate)
        {
            switch (botToUpdate.Role)
            {
                case BotType.bosstagilla:
                    botToUpdate.Data.BotChances.EquipmentChances["FaceCover"] = 100;
                    break;
            }
        }

        public static void ApplyModChanceOverrides(GeneratedBot botToUpdate)
        {
            var weaponMods = botToUpdate.Data.BotChances.WeaponModsChances;
            switch (botToUpdate.Role)
            {
                case BotType.bosskojaniy:
                    weaponMods["mod_stock"] = 100;
                    weaponMods["mod_scope"] = 100;
                    break;
                case BotType.bosstagilla:
                    weaponMods["mod_tactical"] = 100; // force ultima thermal camera
                    weaponMods["mod_stock"] = 100;
                    break;
                case BotType.bossbully:
                    weaponMods["mod_stock"] = 100;
                    break;
                case BotType.bosskilla:
                    weaponMods["mod_stock"] = 100;
                    weaponMods["mod_stock_001"] = 100;
                    break;
                case BotType.bosssanitar:
                    weaponMods["mod_scope"] = 100;
                    break;
                case BotType.pmcbot:
                    weaponMods["mod_stock"] = 100;
                    break;
                case BotType.followerbully:
                    weaponMods["mod_stock"] = 100;
                    weaponMods["mod_stock_000"] = 100;
                    break;
                case BotType.followergluharassault:
                case BotType.followergluharscout:
                case BotType.followergluharsecurity:
                case BotType.followergluharsnipe:
                    weaponMods["mod_stock"] = 100;
                    break;
                case BotType.followerkojaniy:
                    weaponMods["mod_stock"] = 100;
                    break;
                case BotType.sectantpriest:
                    weaponMods["mod_stock"] = 100;
                    break;
                case BotType.sectantwarrior:
                    weaponMods["mod_stock"] = 100;
                    break;
                case BotType.marksman:
                    weaponMods["mod_scope"] = 100;
                    weaponMods["mod_stock"] = 100;
                    break;
                case BotType.exusec:
                    weaponMods["mod_stock"] = 100;
                    weaponMods["mod_stock_000"] = 100;
                    weaponMods["mod_stock_001"] = 100;
                    break;
            }
        }

        public static void AddGenerationChances(GeneratedBot bot, WeightingService weightingService)
        {
            var weightsData = weightingService.GetBotGenerationWeights(bot.Role);
            bot.Data.BotGeneration.Items = new GenerationWeightingItems
            {
                SpecialItems = weightsData["specialItems"],
                Healing = weightsData["healing"],
                Drugs = weightsData["drugs"],
                Stims = weightsData["stims"],
                Food = weightsData["food"],
                Drink = weightsData["drinks"],
                Currency = weightsData["currency"],
                BackpackLoot = weightsData["backpackLoot"],
                PocketLoot = weightsData["pocketLoot"],
                VestLoot = weightsData["vestLoot"],
                Magazines = weightsData["magazines"],
                Grenades = weightsData["grenades"],
            };
        }

        public static void AddEquipmentChances(GeneratedBot bot, Datum baseBot)
        {
            var chances = bot.Data.BotChances.EquipmentChances;
            foreach (var slot in chances.Keys.ToList())
            {
                chances[slot] += baseBot.Inventory.items.Count(x => x.slotId == slot);
            }
        }

        public static void CalculateEquipmentChances(GeneratedBot bot)
        {
            if (bot.BotCount == 0)
            {
                // No bots, don't do anything
                return;
            }

            var chances = bot.Data.BotChances.EquipmentChances;
            foreach (var slot in chances.Keys.ToList())
            {
                chances[slot] = GetPercent(bot.BotCount, (int)chances[slot]);
            }
        }

        private static int GetPercent(int total, int count)
        {
            var percentChance = (int)Math.Ceiling((double)(((200 * count) + 1) / (total * 2)));
            return percentChance > 100 ? 100 : percentChance; // return 100 if value is > 100
        }
    }
}
