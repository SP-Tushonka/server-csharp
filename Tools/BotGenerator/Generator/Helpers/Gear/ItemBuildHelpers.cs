using Common.Models;
using Common.Models.Input;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using DumpItem = Common.Models.Input.Item;
using ServerItem = SPTarkov.Server.Core.Models.Eft.Common.Tables.Item;

namespace Generator.Helpers.Gear
{
    public static class ItemBuildHelpers
    {
        private const string MetalPistolGripWeapon = "56e0598dd2720bb5668b45a6";
        private const string MetalPistolGrip = "56e05a6ed2720bd0748b4567";

        // An item keeps its builds only when the dumps show it often enough and in few enough variants
        private const int MinSamples = 20;
        private const double MaxDistinctShare = 0.1;

        private static readonly HashSet<string> WeaponSlots = ["FirstPrimaryWeapon", "SecondPrimaryWeapon", "Holster"];

        private static readonly HashSet<string> GearSlots =
        [
            "Headwear",
            "ArmorVest",
            "TacticalVest",
            "FaceCover",
            "Earpiece",
            "Eyewear",
            "Backpack",
            "ArmBand",
        ];

        /// <summary>
        ///     Record the complete build of every weapon and every gear item with attachment slots the bot carries
        /// </summary>
        public static void AddBuilds(GeneratedBot botToUpdate, Datum rawBot)
        {
            var items = rawBot.Inventory.items;
            var children = items.Where(item => item.parentId != null).ToLookup(item => item.parentId);
            foreach (var root in items.Where(item => item.parentId == rawBot.Inventory.equipment))
            {
                if (WeaponSlots.Contains(root.slotId))
                {
                    List<ServerItem> tree = [];
                    AddSample(botToUpdate.WeaponBuildSamples, root, AddToBuild(root, null, children, tree, IsWeaponPart), tree);
                }
                else if (GearSlots.Contains(root.slotId) && SlotNames(root._tpl).Count > 0)
                {
                    List<ServerItem> tree = [];
                    AddSample(botToUpdate.EquipmentBuildSamples, root, AddToBuild(root, null, children, tree, IsGearPart), tree);
                }
            }
        }

        /// <summary>
        ///     Keep the builds of items the role only ever carries in a few fixed variants
        /// </summary>
        public static void SelectBuilds(GeneratedBot bot)
        {
            bot.Data.BotInventory.WeaponBuilds = Select(bot.WeaponBuildSamples);
            bot.Data.BotInventory.EquipmentBuilds = Select(bot.EquipmentBuildSamples);
        }

        private static Dictionary<MongoId, List<BotItemBuild>> Select(Dictionary<MongoId, Dictionary<string, ItemBuildSample>> samplesByItem)
        {
            Dictionary<MongoId, List<BotItemBuild>> selected = [];
            foreach (var (itemTpl, samples) in samplesByItem.OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal))
            {
                var total = samples.Values.Sum(sample => sample.Count);
                if (total < MinSamples || samples.Count > total * MaxDistinctShare)
                {
                    continue;
                }

                selected[itemTpl] = samples
                    .OrderByDescending(entry => entry.Value.Count)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new BotItemBuild { Weight = entry.Value.Count, Items = entry.Value.Items })
                    .ToList();
            }

            return selected.Count > 0 ? selected : null;
        }

        private static void AddSample(
            Dictionary<MongoId, Dictionary<string, ItemBuildSample>> samplesByItem,
            DumpItem root,
            string signature,
            List<ServerItem> tree
        )
        {
            var itemTpl = new MongoId(root._tpl);
            if (!samplesByItem.TryGetValue(itemTpl, out var samples))
            {
                samples = [];
                samplesByItem[itemTpl] = samples;
            }

            if (!samples.TryGetValue(signature, out var sample))
            {
                sample = new ItemBuildSample { Items = tree };
                samples[signature] = sample;
            }

            sample.Count++;
        }

        // Adds the item and its parts to the build and returns a key that is equal for structurally equal builds
        private static string AddToBuild(
            DumpItem item,
            string slotId,
            ILookup<string, DumpItem> children,
            List<ServerItem> tree,
            Func<DumpItem, DumpItem, bool> isPart
        )
        {
            var isRoot = tree.Count == 0;
            tree.Add(
                new ServerItem
                {
                    Id = new MongoId(item._id),
                    Template = new MongoId(item._tpl),
                    ParentId = isRoot ? null : item.parentId,
                    SlotId = isRoot ? null : slotId,
                }
            );

            List<string> parts = [];
            foreach (
                var child in children[item._id]
                    .Where(child => isPart(item, child))
                    .OrderBy(child => child.slotId, StringComparer.Ordinal)
                    .ThenBy(child => child._tpl, StringComparer.Ordinal)
            )
            {
                // The dumps name this grip's slot differently from the weapon template
                var childSlot =
                    item._tpl == MetalPistolGripWeapon && child.slotId == "mod_pistol_grip" && child._tpl == MetalPistolGrip
                        ? "mod_pistolgrip"
                        : child.slotId;
                parts.Add($"{childSlot}={AddToBuild(child, childSlot, children, tree, isPart)}");
            }

            return $"{item._tpl}({string.Join(",", parts)})";
        }

        // Rounds are added by the server when it fills magazines and chambers
        private static bool IsWeaponPart(DumpItem parent, DumpItem child)
        {
            return child.slotId != "cartridges"
                && !child.slotId.StartsWith("patron_in_weapon", StringComparison.OrdinalIgnoreCase)
                && !child.slotId.StartsWith("camora", StringComparison.OrdinalIgnoreCase);
        }

        // Only attachment slots count, loot carried in a rig or backpack grid is not part of the build
        private static bool IsGearPart(DumpItem parent, DumpItem child)
        {
            return SlotNames(parent._tpl).Contains(child.slotId);
        }

        private static HashSet<string> SlotNames(string itemTpl)
        {
            var template = ItemTemplateHelper.GetTemplateById(new MongoId(itemTpl));

            return template?.Properties?.Slots?.Select(slot => slot.Name).ToHashSet() ?? [];
        }
    }
}
