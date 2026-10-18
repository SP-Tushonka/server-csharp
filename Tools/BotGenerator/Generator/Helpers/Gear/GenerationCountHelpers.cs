using Common.Models;
using Common.Models.Input;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using DumpItem = Common.Models.Input.Item;

namespace Generator.Helpers.Gear;

/// <summary>
///     Learns how many items of each loot category a role carries from its dumped bots, sorted the way the server sorts them
/// </summary>
public static class GenerationCountHelpers
{
    /// <summary>
    ///     Dumped bots a role needs before its counts replace the hand written weights
    /// </summary>
    public const int MinimumSamples = 4;

    private static readonly string[] _weaponSlots = ["FirstPrimaryWeapon", "SecondPrimaryWeapon", "Holster"];

    private static readonly string[] _categories =
    [
        "healing", "drugs", "stims", "food", "drinks", "currency", "grenades", "magazines", "pocketLoot", "vestLoot", "backpackLoot",
    ];

    /// <summary>
    ///     Count the categorised items one dumped bot carries
    /// </summary>
    /// <param name="bot">Bot type being generated</param>
    /// <param name="rawBot">Dumped bot</param>
    public static void AddCounts(GeneratedBot bot, Datum rawBot)
    {
        var items = rawBot.Inventory.items;
        var equipmentId = rawBot.Inventory.equipment;
        var containers = items
            .Where(item => item.parentId == equipmentId && item.slotId is "Pockets" or "TacticalVest" or "Backpack")
            .ToDictionary(item => item._id, item => item.slotId);

        var counts = _categories.ToDictionary(category => category, _ => 0);
        foreach (var item in items)
        {
            // Plates and inserts sit in a rig's slots, only items with a grid location are loot
            if (item.location is null || item.parentId is null || !containers.TryGetValue(item.parentId, out var container))
            {
                continue;
            }

            var category = Categorise(item._tpl, container);
            if (category is not null)
            {
                counts[category]++;
                if (!category.EndsWith("Loot"))
                {
                    Place(bot, category, container);
                }
            }
        }

        AddMagazinePlacement(bot, items, equipmentId, containers);

        // Spare magazines are those matching the one loaded in the primary weapon
        var primary = items.FirstOrDefault(item => item.parentId == equipmentId && item.slotId == "FirstPrimaryWeapon");
        var loaded = primary is null ? null : items.FirstOrDefault(item => item.parentId == primary._id && item.slotId == "mod_magazine");
        var spareMagazines = loaded is null ? -1 : items.Count(item => item._tpl == loaded._tpl && item._id != loaded._id);

        foreach (var (category, count) in counts)
        {
            if (category == "magazines")
            {
                if (spareMagazines < 0)
                {
                    continue;
                }

                Increment(bot, category, spareMagazines);
                continue;
            }

            Increment(bot, category, count);
        }

        AddSecureCounts(bot, items, equipmentId);
        AddChamberCounts(bot, items, equipmentId);
    }

    // Spare magazines are those matching the one loaded in the weapon
    private static void AddMagazinePlacement(GeneratedBot bot, List<DumpItem> items, string equipmentId, Dictionary<string, string> containers)
    {
        foreach (var slot in _weaponSlots)
        {
            var weapon = items.FirstOrDefault(item => item.parentId == equipmentId && item.slotId == slot);
            var loaded = weapon is null ? null : items.FirstOrDefault(item => item.parentId == weapon._id && item.slotId == "mod_magazine");
            if (loaded is null)
            {
                continue;
            }

            foreach (var spare in items.Where(item => item._tpl == loaded._tpl && item._id != loaded._id && item.parentId is not null))
            {
                if (containers.TryGetValue(spare.parentId!, out var container))
                {
                    Place(bot, slot, container);
                }
            }
        }
    }

    private static void Place(GeneratedBot bot, string kind, string container)
    {
        if (!bot.PlacementCounts.TryGetValue(kind, out var byContainer))
        {
            byContainer = [];
            bot.PlacementCounts[kind] = byContainer;
        }

        byContainer[container] = byContainer.GetValueOrDefault(container) + 1;
    }

    // A magazine fed weapon may spawn with its chamber empty
    private static void AddChamberCounts(GeneratedBot bot, List<DumpItem> items, string equipmentId)
    {
        foreach (var slot in _weaponSlots)
        {
            var weapon = items.FirstOrDefault(item => item.parentId == equipmentId && item.slotId == slot);
            if (weapon is null || !items.Any(item => item.parentId == weapon._id && item.slotId == "mod_magazine"))
            {
                continue;
            }

            var chambered = items.Any(item => item.parentId == weapon._id && item.slotId?.StartsWith("patron_in_weapon") == true);
            Increment(bot, $"chamber/{slot}", chambered ? 1 : 0);
        }
    }

    // The secure container holds full ammo stacks per weapon and a few other items
    private static void AddSecureCounts(GeneratedBot bot, List<DumpItem> items, string equipmentId)
    {
        var secure = items.FirstOrDefault(item => item.parentId == equipmentId && item.slotId == "SecuredContainer");
        if (secure is null)
        {
            return;
        }

        var secureItems = items.Where(item => item.parentId == secure._id && item.location is not null).ToList();
        Increment(bot, "secureLoot", secureItems.Count(item => !IsAmmo(item._tpl)));

        foreach (var slot in _weaponSlots)
        {
            var weapon = items.FirstOrDefault(item => item.parentId == equipmentId && item.slotId == slot);
            var ammoTpl = weapon is null ? null : LoadedAmmo(items, weapon);
            if (ammoTpl is not null)
            {
                Increment(bot, $"secureAmmo/{slot}", secureItems.Count(item => item._tpl == ammoTpl));
            }
        }
    }

    private static string? LoadedAmmo(List<DumpItem> items, DumpItem weapon)
    {
        var magazine = items.FirstOrDefault(item => item.parentId == weapon._id && item.slotId == "mod_magazine");
        var cartridge = magazine is null ? null : items.FirstOrDefault(item => item.parentId == magazine._id);

        return (cartridge ?? items.FirstOrDefault(item => item.parentId == weapon._id && item.slotId?.StartsWith("patron_in_weapon") == true))?._tpl;
    }

    private static bool IsAmmo(string tpl)
    {
        return ItemTemplateHelper.Items.TryGetValue(new MongoId(tpl), out var template) && template.Properties?.AmmoType is not null;
    }

    /// <summary>
    ///     Replace the hand written weights with the learned counts for roles with enough dumped bots
    /// </summary>
    /// <param name="bot">Bot type being generated</param>
    public static void ApplyLearnedWeights(GeneratedBot bot)
    {
        if (bot.BotCount < MinimumSamples)
        {
            return;
        }

        var weights = bot.Data.BotGeneration.Items;
        if (bot.PlacementCounts.Count > 0)
        {
            weights.Placement = bot.PlacementCounts
                .OrderBy(entry => entry.Key)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.OrderByDescending(count => count.Value).ToDictionary(count => count.Key, count => (double)count.Value)
                );
        }

        foreach (var (category, histogram) in bot.GenerationCounts)
        {
            var learned = histogram.OrderBy(entry => entry.Key).ToDictionary(entry => (double)entry.Key, entry => (double)entry.Value);
            if (category.StartsWith("secureAmmo/"))
            {
                weights.SecureAmmoStacks ??= [];
                weights.SecureAmmoStacks[category["secureAmmo/".Length..]] = Learned(null, learned);
                continue;
            }

            if (category.StartsWith("chamber/"))
            {
                weights.ChamberedRounds ??= [];
                weights.ChamberedRounds[category["chamber/".Length..]] = Learned(null, learned);
                continue;
            }

            switch (category)
            {
                case "secureLoot": weights.SecureLoot = Learned(null, learned); break;
                case "healing": weights.Healing = Learned(weights.Healing, learned); break;
                case "drugs": weights.Drugs = Learned(weights.Drugs, learned); break;
                case "stims": weights.Stims = Learned(weights.Stims, learned); break;
                case "food": weights.Food = Learned(weights.Food, learned); break;
                case "drinks": weights.Drink = Learned(weights.Drink, learned); break;
                case "currency": weights.Currency = Learned(weights.Currency, learned); break;
                case "grenades": weights.Grenades = Learned(weights.Grenades, learned); break;
                case "magazines": weights.Magazines = Learned(weights.Magazines, learned); break;
                case "pocketLoot": weights.PocketLoot = Learned(weights.PocketLoot, learned); break;
                case "vestLoot": weights.VestLoot = Learned(weights.VestLoot, learned); break;
                default: weights.BackpackLoot = Learned(weights.BackpackLoot, learned); break;
            }
        }
    }

    // The whitelist stays hand written, only the counts are learned
    private static GenerationData Learned(GenerationData? handWritten, Dictionary<double, double> weights)
    {
        return new GenerationData { Weights = weights, Whitelist = handWritten?.Whitelist ?? [] };
    }

    private static void Increment(GeneratedBot bot, string category, int count)
    {
        if (!bot.GenerationCounts.TryGetValue(category, out var histogram))
        {
            histogram = [];
            bot.GenerationCounts[category] = histogram;
        }

        histogram[count] = histogram.GetValueOrDefault(count) + 1;
    }

    // Mirrors BotLootCacheService, loose rounds go to the secure container and are not loot
    private static string? Categorise(string tpl, string container)
    {
        if (!ItemTemplateHelper.Items.TryGetValue(new MongoId(tpl), out var template))
        {
            return null;
        }

        var properties = template.Properties;
        if (properties?.AmmoType is not null || properties?.ReloadMagType is not null)
        {
            return null;
        }

        if (properties?.MedUseTime is not null)
        {
            return template.Parent == BaseClasses.DRUGS ? "drugs"
                : template.Parent == BaseClasses.STIMULATOR ? "stims"
                : "healing";
        }

        if (properties?.ThrowType is not null)
        {
            return "grenades";
        }

        if (IsOfBaseclass(template, BaseClasses.FOOD))
        {
            return "food";
        }

        if (IsOfBaseclass(template, BaseClasses.DRINK))
        {
            return "drinks";
        }

        if (IsOfBaseclass(template, BaseClasses.MONEY))
        {
            return "currency";
        }

        return container switch
        {
            "Pockets" => "pocketLoot",
            "TacticalVest" => "vestLoot",
            _ => "backpackLoot",
        };
    }

    private static bool IsOfBaseclass(TemplateItem template, MongoId baseClass)
    {
        var current = template;
        while (current is not null)
        {
            if (current.Id == baseClass)
            {
                return true;
            }

            current = ItemTemplateHelper.Items.GetValueOrDefault(current.Parent);
        }

        return false;
    }
}
