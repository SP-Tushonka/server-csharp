using Common.Models;
using Common.Models.Input;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;

namespace Generator.Helpers.Gear
{
    public static class GearHelpers
    {
        public static void AddEquippedMods(GeneratedBot botToUpdate, Datum rawParsedBot)
        {
            var modItemsInRawBot = new List<Item>();
            var itemsWithModsInRawBot = new List<Item>();

            modItemsInRawBot = rawParsedBot
                .Inventory.items.Where(x =>
                    x.slotId != null
                    && (
                        x.slotId.StartsWith("mod_")
                        || x.slotId.ToLower().StartsWith("patron_in_weapon")
                        || x.slotId.ToLower().StartsWith("helmet_")
                        || x.slotId.ToLower().StartsWith("front_")
                        || x.slotId.ToLower().StartsWith("back_")
                        || x.slotId.ToLower().StartsWith("collar")
                        || x.slotId.ToLower().StartsWith("groin")
                        || x.slotId.ToLower().StartsWith("left")
                        || x.slotId.ToLower().StartsWith("right")
                        || x.slotId.ToLower().StartsWith("soft_")
                        || x.slotId.ToLower().StartsWith("shoulder")
                    )
                )
                .ToList();

            // Get items with Mods by iterating over mod items and getting the parent item
            itemsWithModsInRawBot.AddRange(
                modItemsInRawBot.Select(modItem => rawParsedBot.Inventory.items.Find(x => x._id == modItem.parentId))
            );

            var itemsWithModsDictionary = botToUpdate.Data.BotInventory.Mods;
            foreach (var itemToAdd in itemsWithModsInRawBot)
            {
                var modsToAdd = modItemsInRawBot.Where(x => x.parentId == itemToAdd._id).ToList();

                // fix pistolgrip that changes slot id name
                if (itemToAdd._tpl == "56e0598dd2720bb5668b45a6")
                {
                    var badMod = modsToAdd.FirstOrDefault(x => x.slotId == "mod_pistol_grip" && x._tpl == "56e05a6ed2720bd0748b4567");
                    if (badMod != null)
                    {
                        badMod.slotId = "mod_pistolgrip";
                    }
                }

                AddItemToDictionary(itemToAdd, modsToAdd, itemsWithModsDictionary);

                // check if these mods have sub-mods and add those
                foreach (var modAdded in modsToAdd.Where(x => x.slotId == "mod_magazine"))
                {
                    // look for items where parentId is this mods id
                    var subItems = rawParsedBot.Inventory.items.Where(x => x.parentId == modAdded._id && x.slotId != "cartridges").ToList();
                    if (subItems.Count > 0)
                    {
                        AddItemToDictionary(modAdded, subItems, itemsWithModsDictionary);
                    }
                }
            }
        }

        internal static void AddAmmo(GeneratedBot botToUpdate, Datum bot)
        {
            var ammoPool = botToUpdate.Data.BotInventory.Ammo;
            foreach (
                var ammo in bot.Inventory.items.Where(x =>
                    x.slotId != null
                    && (
                        x.slotId == "patron_in_weapon"
                        || (
                            x.slotId == "cartridges"
                            && bot.Inventory.items.FirstOrDefault(parent => parent._id == x.parentId)?.slotId != "main"
                        ) // Ignore cartridges in ammo boxes for ammo usage calc
                        || x.slotId.StartsWith("camora")
                    )
                )
            )
            {
                var props = ItemTemplateHelper.GetTemplateById(ammo._tpl).Properties;
                var caliber = props.AmmoCaliber ?? props.Caliber;

                // Create key if caliber doesnt exist
                if (!ammoPool.ContainsKey(caliber))
                {
                    ammoPool[caliber] = new Dictionary<MongoId, double>();
                }

                IncrementDictionaryValue(ammoPool[caliber], ammo._tpl);
            }
        }

        public static int CommonDivisor(List<int> numbers)
        {
            int result = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                result = GCD(result, numbers[i]);
            }
            return result;
        }

        private static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        // Rig and backpack grids are named with digits, which Enum.TryParse would read as slot numbers
        private static readonly Dictionary<string, EquipmentSlots> EquipmentSlotNames = Enum.GetValues<EquipmentSlots>()
            .ToDictionary(slot => slot.ToString(), slot => slot, StringComparer.OrdinalIgnoreCase);

        public static void AddEquippedGear(GeneratedBot botToUpdate, Datum bot)
        {
            var equipment = botToUpdate.Data.BotInventory.Equipment;
            foreach (var inventoryItem in bot.Inventory.items.Where(x => x.slotId != null))
            {
                if (EquipmentSlotNames.TryGetValue(inventoryItem.slotId, out var slot) && equipment.TryGetValue(slot, out var pool))
                {
                    IncrementDictionaryValue(pool, inventoryItem._tpl);
                }
            }
        }

        public static void IncrementDictionaryValue(Dictionary<MongoId, double> dictToIncrement, MongoId key)
        {
            if (!dictToIncrement.ContainsKey(key))
            {
                dictToIncrement[key] = 0;
            }

            dictToIncrement[key]++;
        }

        public static void AddCartridges(GeneratedBot botToUpdate, Datum rawParsedBot)
        {
            var cartridgesInRawBot = rawParsedBot.Inventory.items.Where(x => x.slotId?.StartsWith("cartridges") == true).ToList();
            var cartridgeParentIds = cartridgesInRawBot.Select(x => x.parentId).ToList();
            var itemsThatTakeCartridges = rawParsedBot.Inventory.items.Where(x => cartridgeParentIds.Contains(x._id)).ToList();

            var itemsThatTakeCartridgesDict = CreateDictionaryPopulateWithMagazinesAndCartridges(
                itemsThatTakeCartridges,
                cartridgesInRawBot
            );
            var mods = botToUpdate.Data.BotInventory.Mods;
            foreach (var item in itemsThatTakeCartridgesDict)
            {
                // Item exists update
                if (mods.TryGetValue(item.Key, out var existingMagazine))
                {
                    existingMagazine["cartridges"].UnionWith(item.Value);
                }
                else // No item found, add fresh
                {
                    mods.Add(item.Key, new Dictionary<string, HashSet<MongoId>> { { "cartridges", item.Value } });
                }
            }
        }

        private static void AddItemToDictionary(
            Item itemToAdd,
            List<Item> modsToAdd,
            Dictionary<MongoId, Dictionary<string, HashSet<MongoId>>> itemsWithModsDict
        )
        {
            if (!itemsWithModsDict.TryGetValue(itemToAdd._tpl, out var itemMods))
            {
                itemMods = new Dictionary<string, HashSet<MongoId>>();
                itemsWithModsDict.Add(itemToAdd._tpl, itemMods);
            }

            foreach (var modItem in modsToAdd)
            {
                if (!itemMods.TryGetValue(modItem.slotId, out var modTpls))
                {
                    modTpls = new HashSet<MongoId>();
                    itemMods.Add(modItem.slotId, modTpls);
                }

                modTpls.Add(modItem._tpl);
            }
        }

        private static Dictionary<MongoId, HashSet<MongoId>> CreateDictionaryPopulateWithMagazinesAndCartridges(
            List<Item> itemsThatTakeCartridges,
            List<Item> cartridgesInRawBot
        )
        {
            var itemsThatTakeCartridgesDict = new Dictionary<MongoId, HashSet<MongoId>>();
            foreach (var item in itemsThatTakeCartridges)
            {
                var cartridgeIdsToAdd = cartridgesInRawBot.Where(x => x.parentId == item._id).Select(x => (MongoId)x._tpl);

                if (itemsThatTakeCartridgesDict.TryGetValue(item._tpl, out var existingMagazine))
                {
                    existingMagazine.UnionWith(cartridgeIdsToAdd);
                }
                else
                {
                    itemsThatTakeCartridgesDict.Add(item._tpl, cartridgeIdsToAdd.ToHashSet());
                }
            }

            return itemsThatTakeCartridgesDict;
        }

        internal static void ReduceAmmoWeightValues(GeneratedBot botToUpdate)
        {
            var ammoPool = botToUpdate.Data.BotInventory.Ammo;
            foreach (var caliber in ammoPool)
            {
                foreach (var cartridge in ammoPool.Keys)
                {
                    var cartridgeWithWeights = ammoPool[cartridge];
                    foreach (var cartridgeKvP in cartridgeWithWeights)
                    {
                        cartridgeWithWeights[cartridgeKvP.Key] = ReduceValueAccuracy((long)cartridgeKvP.Value);
                    }

                    var weights = cartridgeWithWeights.Values.Select(x => (int)x).ToList();

                    var commonAmmoDivisor = CommonDivisor(weights);

                    foreach (var cartridgeWeightKvP in cartridgeWithWeights)
                    {
                        ammoPool[cartridge][cartridgeWeightKvP.Key] = (int)cartridgeWeightKvP.Value / commonAmmoDivisor;
                    }
                }
            }
        }

        public static void ReduceEquipmentWeightValues(Dictionary<EquipmentSlots, Dictionary<MongoId, double>> equipment)
        {
            foreach (var pool in equipment.Values)
            {
                ReduceWeightValues(pool);
            }
        }

        public static void ReduceWeightValues(Dictionary<MongoId, double> equipmentDict)
        {
            // No values, nothing to reduce
            if (equipmentDict.Count == 0)
            {
                return;
            }

            // Only one value, quickly set to 1 and exit
            if (equipmentDict.Count == 1)
            {
                equipmentDict[equipmentDict.First().Key] = 1;

                return;
            }

            foreach (var itemWeightKvp in equipmentDict)
            {
                equipmentDict[itemWeightKvp.Key] = ReduceValueAccuracy((long)itemWeightKvp.Value, 4);
            }

            var weights = equipmentDict.Values.Select(x => (int)x).ToList();
            var commonAmmoDivisor = CommonDivisor(weights);

            // No point in dividing by 1
            if (commonAmmoDivisor == 1)
            {
                return;
            }

            foreach (var itemTplWithWeight in equipmentDict)
            {
                equipmentDict[itemTplWithWeight.Key] = (int)itemTplWithWeight.Value / commonAmmoDivisor;
            }
        }

        private static int ReduceValueAccuracy(long x, int digits = 3)
        {
            int i = (int)Math.Log10(x);
            i = Math.Max(0, i - (digits - 1));
            i = (int)Math.Pow(10, i);
            return (int)(x / i * i);
        }
    }
}
