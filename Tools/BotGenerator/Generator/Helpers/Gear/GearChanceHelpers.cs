using System.Collections.Concurrent;
using Common.Models;
using Common.Models.Input;
using Generator.Weighting;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
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

            var modCounts = weaponModCount.GetOrAdd(bot.Key, _ => new Dictionary<string, int>());
            var slotCounts = weaponSlotCount.GetOrAdd(bot.Key, _ => new Dictionary<string, int>());

            CountSlotsAndMods(baseBot, validSlots, modCounts, slotCounts);
        }

        public static void CalculateModChances(GeneratedBot bot)
        {
            var modCounts = weaponModCount.GetOrAdd(bot.Key, _ => new Dictionary<string, int>());
            var slotCounts = weaponSlotCount.GetOrAdd(bot.Key, _ => new Dictionary<string, int>());

            bot.Data.BotChances.WeaponModsChances = slotCounts.ToDictionary(
                kvp => kvp.Key,
                kvp => (double)GetPercent(kvp.Value, modCounts.GetValueOrDefault(kvp.Key))
            );
        }

        public static void AddEquipmentModChances(GeneratedBot bot, Datum baseBot)
        {
            var modCounts = equipmentModCount.GetOrAdd(bot.Key, _ => new Dictionary<string, int>());
            var slotCounts = equipmentSlotCount.GetOrAdd(bot.Key, _ => new Dictionary<string, int>());

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
            var modCounts = equipmentModCount.GetOrAdd(bot.Key, _ => new Dictionary<string, int>());
            var slotCounts = equipmentSlotCount.GetOrAdd(bot.Key, _ => new Dictionary<string, int>());

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

            CountUnblockedSlots(bot, baseBot);

            var hasPrimary = baseBot.Inventory.items.Any(x => x.slotId == "FirstPrimaryWeapon");
            if (hasPrimary)
            {
                bot.PrimaryWeaponCount++;
                if (baseBot.Inventory.items.Any(x => x.slotId == "Holster"))
                {
                    bot.HolsterWithPrimaryCount++;
                }
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
                var eligible = UnblockedBotCount(bot, slot);
                chances[slot] = eligible > 0 ? GetPercent(eligible, (int)chances[slot]) : 0;
            }

            // The server always gives a bot without a primary a pistol, so the holster chance only covers bots with one
            if (bot.PrimaryWeaponCount > 0)
            {
                chances["Holster"] = GetPercent(bot.PrimaryWeaponCount, bot.HolsterWithPrimaryCount);
            }
        }

        // The server rolls these slots in this order and skips one that an item already worn blocks or conflicts with
        private static readonly string[] _blockableSlotOrder = ["FaceCover", "Headwear", "Earpiece", "ArmorVest", "TacticalVest"];

        // Record what was worn before each empty slot, the slot's pool is only complete once every dump is read
        private static void CountUnblockedSlots(GeneratedBot bot, Datum baseBot)
        {
            var equipped = baseBot
                .Inventory.items.Where(x => x.parentId == baseBot.Inventory.equipment && x.slotId is not null)
                .GroupBy(x => x.slotId)
                .ToDictionary(group => group.Key, group => group.First()._tpl);

            for (var i = 0; i < _blockableSlotOrder.Length; i++)
            {
                var slot = _blockableSlotOrder[i];
                if (equipped.ContainsKey(slot))
                {
                    continue;
                }

                var wornBefore = string.Join(
                    ",",
                    _blockableSlotOrder.Take(i).Where(equipped.ContainsKey).Select(earlier => equipped[earlier]).Order()
                );
                if (wornBefore.Length == 0)
                {
                    continue;
                }

                if (!bot.EmptySlotsAfter.TryGetValue(slot, out var byWorn))
                {
                    byWorn = [];
                    bot.EmptySlotsAfter[slot] = byWorn;
                }

                byWorn[wornBefore] = byWorn.GetValueOrDefault(wornBefore) + 1;
            }
        }

        // Bots that could have worn the slot, those whose earlier items blocked every item of the slot's pool are left out
        private static int UnblockedBotCount(GeneratedBot bot, string slot)
        {
            if (!bot.EmptySlotsAfter.TryGetValue(slot, out var byWorn) || !Enum.TryParse<EquipmentSlots>(slot, out var equipmentSlot))
            {
                return bot.BotCount;
            }

            var pool = bot.Data.BotInventory.Equipment.GetValueOrDefault(equipmentSlot)?.Keys.ToList() ?? [];
            var blocked = byWorn.Where(entry => entry.Key.Split(',').Any(tpl => BlocksWholePool(tpl, slot, pool))).Sum(entry => entry.Value);

            return bot.BotCount - blocked;
        }

        private static bool BlocksWholePool(string tpl, string slot, List<MongoId> pool)
        {
            var template = ItemTemplateHelper.Items.GetValueOrDefault(new MongoId(tpl));
            if (template is null)
            {
                return false;
            }

            if (template.Blocks.GetValueOrDefault(slot) || (slot == "ArmorVest" && template.Properties?.BlocksArmorVest == true))
            {
                return true;
            }

            return pool.Count > 0
                && pool.All(candidate =>
                    (template.Properties?.ConflictingItems?.Contains(candidate) ?? false)
                    || (ItemTemplateHelper.Items.GetValueOrDefault(candidate)?.Properties?.ConflictingItems?.Contains(template.Id) ?? false)
                );
        }

        private static int GetPercent(int total, int count)
        {
            var percentChance = (int)Math.Ceiling((double)(((200 * count) + 1) / (total * 2)));
            return percentChance > 100 ? 100 : percentChance; // return 100 if value is > 100
        }
    }
}
