using System.Diagnostics;
using System.Linq;
using Common.Extensions;
using Common.Models;
using Common.Models.Input;
using Generator.Helpers.Gear;
using SPTarkov.Server.Core.Models.Common;

namespace Generator
{
    public static class BotLootGenerator
    {
        internal static void AddLoot(GeneratedBot botToUpdate, Datum rawBotData)
        {
            AddLootToContainers(botToUpdate, rawBotData);
        }

        private static void AddLootToContainers(GeneratedBot botToUpdate, Datum rawBot)
        {
            // Filter out base inventory items and equipment mod items
            var rawBotItems = rawBot.Inventory.items.Where(item => item.location != null);

            var botBackpack = rawBot.Inventory.items.FirstOrDefault(item => item.slotId == "Backpack");
            if (botBackpack != null)
            {
                AddLootItemsToContainerDictionary(
                    rawBotItems,
                    botBackpack._id,
                    botToUpdate.Data.BotInventory.Items.Backpack,
                    "backpack",
                    botToUpdate.Role
                );
            }

            var botPockets = rawBot.Inventory.items.FirstOrDefault(item => item.slotId == "Pockets");
            if (botPockets != null)
            {
                AddLootItemsToContainerDictionary(rawBotItems, botPockets._id, botToUpdate.Data.BotInventory.Items.Pockets);
            }

            var botVest = rawBot.Inventory.items.FirstOrDefault(item => item.slotId == "TacticalVest");
            if (botVest != null)
            {
                AddLootItemsToContainerDictionary(rawBotItems, botVest._id, botToUpdate.Data.BotInventory.Items.TacticalVest);
            }

            var botSecure = rawBot.Inventory.items.FirstOrDefault(item => item.slotId == "SecuredContainer");
            if (botSecure != null)
            {
                AddLootItemsToContainerDictionary(rawBotItems, botSecure._id, botToUpdate.Data.BotInventory.Items.SecuredContainer);
            }

            // Add generic keys to bosses
            if (botToUpdate.Role.IsBoss())
            {
                var keys = SpecialLootHelper.GetGenericBossKeysDictionary();
                foreach (var bosskey in keys)
                {
                    if (!botToUpdate.Data.BotInventory.Items.Backpack.ContainsKey(bosskey.Key))
                    {
                        botToUpdate.Data.BotInventory.Items.Backpack.Add(bosskey.Key, bosskey.Value);
                    }
                }
            }

            AddSpecialLoot(botToUpdate);

            // Cleanup of weights
            GearHelpers.ReduceWeightValues(botToUpdate.Data.BotInventory.Items.Backpack);
            GearHelpers.ReduceWeightValues(botToUpdate.Data.BotInventory.Items.Pockets);
            GearHelpers.ReduceWeightValues(botToUpdate.Data.BotInventory.Items.TacticalVest);
            GearHelpers.ReduceWeightValues(botToUpdate.Data.BotInventory.Items.SecuredContainer);
        }

        /// <summary>
        /// Look for items inside itemsToFilter that have the parentid of `containerId` and add them to dictToAddTo
        /// Keep track of how many items are added in the dictToAddTo value
        /// </summary>
        /// <param name="itemsToFilter">Bots inventory items</param>
        /// <param name="containerId"></param>
        /// <param name="dictToAddTo"></param>
        private static void AddLootItemsToContainerDictionary(
            IEnumerable<Common.Models.Input.Item> itemsToFilter,
            string containerId,
            Dictionary<MongoId, double> dictToAddTo,
            string container = "",
            BotType type = BotType.arenafighterevent
        )
        {
            foreach (var itemToAdd in itemsToFilter)
            {
                if (itemToAdd.parentId != containerId)
                    continue;

                if (!dictToAddTo.ContainsKey(itemToAdd._tpl))
                {
                    dictToAddTo[itemToAdd._tpl] = 1;

                    continue;
                }

                dictToAddTo[itemToAdd._tpl]++;
            }
        }

        private static void AddSpecialLoot(GeneratedBot botToUpdate)
        {
            var itemsToAdd = SpecialLootHelper.GetSpecialLootForBotType(botToUpdate.Role);
            foreach (var item in itemsToAdd)
            {
                if (!botToUpdate.Data.BotInventory.Items.SpecialLoot.ContainsKey(item.Key))
                {
                    botToUpdate.Data.BotInventory.Items.SpecialLoot.Add(item.Key, item.Value);
                }
            }
        }
    }
}
