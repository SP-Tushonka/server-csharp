using Microsoft.Extensions.Logging;
using SPTarkov.Common.Extensions;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Services.Server.SeasonalEvents;

/// <summary>
///     The individual changes a seasonal event can make to the loaded database. Each
///     <see cref="ISeasonalEventApplier" /> composes the ones its event needs, so an effect shared by two
///     events is written once here.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class SeasonalEventEffects(
    ISptLogger<SeasonalEventEffects> logger,
    GlobalTable globalTable,
    BotTable botTable,
    LocationTable locationTable,
    ServerLocalisationService serverLocalisationService,
    TradersTable tradersTable,
    BotConfig botConfig,
    LocationConfig locationConfig,
    SeasonalEventConfig seasonalEventConfig
)
{
    /// <summary>
    ///     Adjust the weights for all bots body part appearances, based on data inside
    ///     seasonalevents.json/botAppearanceChanges
    /// </summary>
    /// <param name="season">Season to apply changes for</param>
    public void AdjustBotAppearanceValues(SeasonalEventType season)
    {
        if (!seasonalEventConfig.BotAppearanceChanges.TryGetValue(season, out var appearanceAdjustments))
        {
            // No changes found for this season
            return;
        }

        foreach (var (botType, botAppearanceAdjustments) in appearanceAdjustments)
        {
            if (!botTable.Types.TryGetValue(botType, out var bot))
            {
                // Bot defined in config doesn't exist
                continue;
            }

            foreach (var (bodyPart, weightAdjustments) in botAppearanceAdjustments)
            {
                // Get the matching bots appearance pool by key
                var partPool = bodyPart switch
                {
                    "body" => bot.BotAppearance.Body,
                    "feet" => bot.BotAppearance.Feet,
                    "hands" => bot.BotAppearance.Hands,
                    "head" => bot.BotAppearance.Head,
                    _ => null,
                };

                if (partPool is null)
                {
                    logger.Warning($"Unable to adjust bot: {botType} body part appearance: {bodyPart}");

                    continue;
                }

                // Apply new weights to values from config
                foreach (var (itemId, weighting) in weightAdjustments)
                {
                    partPool[itemId] = weighting;
                }
            }
        }
    }

    public void ReplaceBotHostility(
        Dictionary<string, List<AdditionalHostilitySettings>> hostilitySettings,
        HashSet<string>? locationWhitelist = null
    )
    {
        var locations = locationTable.GetDictionary();
        var ignoreList = locationConfig.NonMaps;

        foreach (var (locationName, locationBase) in locations)
        {
            if (ignoreList.Contains(locationName))
            {
                continue;
            }

            if (locationBase?.Base?.BotLocationModifier?.AdditionalHostilitySettings is null)
            {
                continue;
            }

            // Try for location-specific hostility settings first
            if (!hostilitySettings.TryGetValue(locationBase.Base.Id.ToLowerInvariant(), out var newHostilitySettings))
            {
                // If we don't have location-specific, fall back to defaults
                if (!hostilitySettings.TryGetValue("default", out newHostilitySettings))
                {
                    // No settings by map, or default fallback, skip map
                    continue;
                }
            }

            if (locationWhitelist is not null && !locationWhitelist.Contains(locationBase.Base.Id.ToLowerInvariant()))
            {
                continue;
            }

            foreach (var settings in newHostilitySettings)
            {
                var matchingBaseSettings = locationBase.Base.BotLocationModifier?.AdditionalHostilitySettings?.FirstOrDefault(x =>
                    x.BotRole == settings.BotRole
                );
                if (matchingBaseSettings is null)
                {
                    // Doesn't exist, add it
                    locationBase.Base.BotLocationModifier.AdditionalHostilitySettings = [.. locationBase.Base.BotLocationModifier.AdditionalHostilitySettings, settings];

                    continue;
                }

                if (settings.AlwaysEnemies is not null)
                {
                    matchingBaseSettings.AlwaysEnemies = settings.AlwaysEnemies;
                }

                if (settings.AlwaysFriends is not null)
                {
                    matchingBaseSettings.AlwaysFriends = settings.AlwaysFriends;
                }

                if (settings.BearEnemyChance is not null)
                {
                    matchingBaseSettings.BearEnemyChance = settings.BearEnemyChance;
                }

                if (settings.ChancedEnemies is not null)
                {
                    matchingBaseSettings.ChancedEnemies = settings.ChancedEnemies;
                }

                if (settings.Neutral is not null)
                {
                    matchingBaseSettings.Neutral = settings.Neutral;
                }

                if (settings.SavageEnemyChance is not null)
                {
                    matchingBaseSettings.SavageEnemyChance = settings.SavageEnemyChance;
                }

                if (settings.SavagePlayerBehaviour is not null)
                {
                    matchingBaseSettings.SavagePlayerBehaviour = settings.SavagePlayerBehaviour;
                }

                if (settings.UsecEnemyChance is not null)
                {
                    matchingBaseSettings.UsecEnemyChance = settings.UsecEnemyChance;
                }

                if (settings.UsecPlayerBehaviour is not null)
                {
                    matchingBaseSettings.UsecPlayerBehaviour = settings.UsecPlayerBehaviour;
                }

                if (settings.Warn is not null)
                {
                    matchingBaseSettings.Warn = settings.Warn;
                }
            }
        }
    }

    public void RemoveEntryRequirement(IEnumerable<string> locationIds)
    {
        foreach (var locationId in locationIds)
        {
            var location = locationTable.GetLocation(locationId);
            location.Base.AccessKeys = [];
            location.Base.AccessKeysPvE = [];
        }
    }

    public void ConfigureZombies(ZombieSettings zombieSettings)
    {
        var infectionHalloween = globalTable.Configuration.SeasonActivity.InfectionHalloween;
        infectionHalloween.DisplayUIEnabled = true;
        infectionHalloween.Enabled = true;

        var globalInfectionDict = globalTable.LocationInfection;
        foreach (var (locationId, infectionPercentage) in zombieSettings.MapInfectionAmount)
        {
            if (logger.IsLogEnabled(LogLevel.Debug))
            {
                logger.Debug($"Percent infected from map: {locationId} is: {infectionPercentage}");
            }
            // Infection rates sometimes apply to multiple maps, e.g. Factory day/night or Sandbox variants
            // Get the list of maps that should have infection value applied to their base
            // 90% of locations are just 1 map e.g. bigmap = customs
            var mappedLocations = GetLocationFromInfectedLocation(locationId.ToLowerInvariant());
            foreach (var locationKey in mappedLocations)
            {
                locationTable.GetLocation(locationKey).Base.Events.Halloween2024.InfectionPercentage = infectionPercentage;
            }

            // Globals data needs value updated too
            globalInfectionDict[locationId] = Convert.ToInt32(infectionPercentage);
        }

        foreach (var locationId in zombieSettings.DisableBosses)
        {
            locationTable.GetLocation(locationId).Base.BossLocationSpawn = [];
        }

        foreach (var locationId in zombieSettings.DisableWaves)
        {
            locationTable.GetLocation(locationId).Base.Waves = [];
        }

        ScaleBossChancesByInfection(zombieSettings);

        var locationsWithActiveInfection = GetLocationsWithZombies(zombieSettings.MapInfectionAmount);
        AddEventBossesToMaps("halloweenzombies", locationsWithActiveInfection);
    }

    /// <summary>
    ///     Get location ids of maps with an infection above 0
    /// </summary>
    /// <param name="locationInfections">Dict of locations with their infection percentage</param>
    /// <returns>List of lowercased location ids</returns>
    public HashSet<string> GetLocationsWithZombies(Dictionary<string, double> locationInfections)
    {
        var result = new HashSet<string>();

        // Get only the locations with an infection above 0
        var infectionKeys = locationInfections.Where(location => locationInfections[location.Key] > 0);

        // Convert the infected location id into its generic location id
        foreach (var location in infectionKeys)
        {
            result.UnionWith(GetLocationFromInfectedLocation(location.Key.ToLowerInvariant()));
        }

        return result;
    }

    /// <summary>
    ///     BSG store the location ids differently inside `LocationInfection`, need to convert to matching location IDs
    /// </summary>
    /// <param name="infectedLocationKey">Key to convert</param>
    /// <returns>List of locations</returns>
    public List<string> GetLocationFromInfectedLocation(string infectedLocationKey)
    {
        return infectedLocationKey switch
        {
            "factory4" => ["factory4_day", "factory4_night"],
            "sandbox" => ["sandbox", "sandbox_high", "sandbox_start"],
            _ => [infectedLocationKey],
        };
    }

    public void AddEventWavesToMaps(string eventType)
    {
        var wavesToAddByMap = seasonalEventConfig.EventWaves[eventType.ToLowerInvariant()];

        if (wavesToAddByMap is null)
        {
            logger.Warning($"Unable to add: {eventType} waves, eventWaves is missing");
            return;
        }

        var locations = locationTable.GetAllPropertiesAsDictionary();
        foreach (var map in wavesToAddByMap)
        {
            var wavesToAdd = wavesToAddByMap[map.Key];
            if (wavesToAdd is null)
            {
                logger.Warning($"Unable to add: {eventType} wave to: {map.Key}");
                continue;
            }

            ((Location)locations[map.Key]).Base.Waves = [];
            ((Location)locations[map.Key]).Base.Waves.AddRange(wavesToAdd);
        }
    }

    /// <summary>
    ///     Add event bosses to maps
    /// </summary>
    /// <param name="eventType">Seasonal event, e.g. HALLOWEEN/CHRISTMAS</param>
    /// <param name="mapIdWhitelist">OPTIONAL - Maps to add bosses to</param>
    public void AddEventBossesToMaps(string eventType, HashSet<string>? mapIdWhitelist = null)
    {
        if (!seasonalEventConfig.EventBossSpawns.TryGetValue(eventType.ToLowerInvariant(), out var botsToAddPerMap))
        {
            logger.Warning($"Unable to add: {eventType} bosses, eventBossSpawns is missing");
            return;
        }

        var locations = locationTable.GetAllPropertiesAsDictionary();
        foreach (var (locationKey, bossesToAdd) in botsToAddPerMap)
        {
            if (bossesToAdd.Count == 0)
            {
                continue;
            }

            if (mapIdWhitelist is not null && !mapIdWhitelist.Contains(locationKey))
            {
                continue;
            }

            var locationName = locationTable.GetMappedKey(locationKey);
            var mapBosses = ((Location)locations[locationName]).Base.BossLocationSpawn;
            var existingBosses = mapBosses.Where(bossSpawn => bossSpawn.TriggerName != "botEvent").Select(bossSpawn => bossSpawn.BossName).ToHashSet();
            foreach (var boss in bossesToAdd)
            {
                // Don't re-add bosses the map already has. An event can list the same one several times
                if (!existingBosses.Contains(boss.BossName))
                {
                    // Boss doesn't exist in maps boss list yet, add
                    mapBosses.Add(boss);
                }
            }
        }
    }

    /// <summary>
    ///     Show the event's icons on traders. Only the avatar path changes, so the icons return when the event ends
    /// </summary>
    /// <param name="avatars">Avatar path by trader id</param>
    public void ApplyTraderAvatars(Dictionary<MongoId, string> avatars)
    {
        foreach (var (traderId, avatar) in avatars)
        {
            var trader = tradersTable.GetTrader(traderId);
            if (trader is null)
            {
                logger.Warning($"Unable to change avatar of trader: {traderId} as they do not exist");
                continue;
            }

            trader.Base.Avatar = avatar;
        }
    }

    /// <summary>
    ///     Lower every spawn chance on an infected map by its infection, rounding up. Event only maps sit at 100 and
    ///     already hold their own values
    /// </summary>
    /// <param name="zombieSettings">Infection amounts and the bosses it leaves alone</param>
    public void ScaleBossChancesByInfection(ZombieSettings zombieSettings)
    {
        var unscaled = zombieSettings.UnscaledBosses ?? [];
        foreach (var (locationId, infection) in zombieSettings.MapInfectionAmount ?? [])
        {
            if (infection is <= 0 or >= 100)
            {
                continue;
            }

            foreach (var locationKey in GetLocationFromInfectedLocation(locationId.ToLowerInvariant()))
            {
                foreach (var spawn in locationTable.GetLocation(locationKey).Base.BossLocationSpawn.Where(spawn => !unscaled.Contains(spawn.BossName)))
                {
                    spawn.BossChance = Math.Ceiling((spawn.BossChance ?? 0) * (100 - infection) / 100);
                }
            }
        }
    }

    /// <summary>
    ///     Apply what an event changes on individual maps
    /// </summary>
    /// <param name="overrides">Changes by location id</param>
    public void ApplyLocationOverrides(Dictionary<string, EventLocationOverride> overrides)
    {
        foreach (var (locationId, change) in overrides)
        {
            var location = locationTable.GetLocation(locationId).Base;
            if (change.AdditionalHostilitySettings is not null)
            {
                location.BotLocationModifier.AdditionalHostilitySettings = change.AdditionalHostilitySettings;
            }

            if (change.ItemCaps is not null)
            {
                location.MaxItemCountInLocation = [.. location.MaxItemCountInLocation ?? [], .. change.ItemCaps];
            }

            foreach (var spawn in location.BossLocationSpawn.Where(spawn => change.ZeroChanceBosses?.Contains(spawn.BossName) == true))
            {
                spawn.BossChance = 0;
            }
        }
    }

    /// <summary>
    ///     Add lootable items from backpack into patrol.ITEMS_TO_DROP difficulty property
    /// </summary>
    public void AddLootItemsToGifterDropItemsList()
    {
        var gifterBot = botTable.Types["gifter"];
        string[] difficulties = ["easy", "normal", "hard", "impossible"];

        foreach (var difficulty in difficulties)
        {
            var gifterPatrolValues = gifterBot.BotDifficulty[difficulty].Patrol;

            // Read existing value from property
            var existingItems = string.IsNullOrWhiteSpace(gifterPatrolValues.ItemsToDrop)
                ? Enumerable.Empty<string>()
                : gifterPatrolValues.ItemsToDrop.Split(',');

            // Merge existing and new tpls we want
            var combinedItems = new HashSet<string>(existingItems);
            combinedItems.UnionWith(gifterBot.BotInventory.Items.Backpack.Keys.Select(x => x.ToString()));

            // Turn set into a comma separated list ready for insertion
            gifterPatrolValues.ItemsToDrop = string.Join(",", combinedItems);
        }
    }

    /// <summary>
    ///     Read in data from seasonalEvents.json and add found equipment items to bots
    /// </summary>
    /// <param name="eventType">Name of the event to read equipment in from config</param>
    public void AddEventGearToBots(SeasonalEventType eventType)
    {
        var botGearChanges = seasonalEventConfig.EventGear.GetValueOrDefault(eventType, null);
        if (botGearChanges is null)
        {
            logger.Warning(serverLocalisationService.GetText("gameevent-no_gear_data", eventType));

            return;
        }

        // Iterate over bots with changes to apply
        foreach (var botKvP in botGearChanges)
        {
            if (!botTable.Types.TryGetValue(botKvP.Key.ToLowerInvariant(), out var botToUpdate) || botToUpdate is null)
            {
                logger.Warning(serverLocalisationService.GetText("gameevent-bot_not_found", botKvP));
                continue;
            }

            // Iterate over each equipment slot change
            var gearAmendmentsBySlot = botGearChanges[botKvP.Key];
            foreach (var equipmentKvP in gearAmendmentsBySlot)
            {
                // Adjust slots spawn chance to be at least 75%
                botToUpdate.BotChances.EquipmentChances[equipmentKvP.Key] = Math.Max(
                    botToUpdate.BotChances.EquipmentChances[equipmentKvP.Key],
                    75
                );

                // Grab gear to add and loop over it
                foreach (var itemToAddKvP in equipmentKvP.Value)
                {
                    var equipmentSlot = (EquipmentSlots)Enum.Parse(typeof(EquipmentSlots), equipmentKvP.Key);
                    var equipmentDict = botToUpdate.BotInventory.Equipment[equipmentSlot];
                    equipmentDict[itemToAddKvP.Key] = equipmentKvP.Value[itemToAddKvP.Key];
                }
            }
        }
    }

    /// <summary>
    ///     Replace what roles wear while the event runs. Normal uses the role's own pools, other difficulties their variant
    /// </summary>
    /// <param name="eventType">Event to read loadouts for</param>
    public void ApplyEventLoadouts(SeasonalEventType eventType)
    {
        if (seasonalEventConfig.EventLoadouts?.TryGetValue(eventType, out var loadouts) != true)
        {
            return;
        }

        foreach (var (role, byDifficulty) in loadouts)
        {
            if (!botTable.Types.TryGetValue(role.ToLowerInvariant(), out var bot) || bot is null)
            {
                logger.Warning(serverLocalisationService.GetText("gameevent-bot_not_found", role));
                continue;
            }

            foreach (var (difficulty, loadout) in byDifficulty)
            {
                BotTypeVariant? variant = null;
                bot.DifficultyVariants?.TryGetValue(difficulty, out variant);
                var inventory = variant?.BotInventory ?? bot.BotInventory;
                var chances = variant?.BotChances ?? bot.BotChances;
                foreach (var (slot, pool) in loadout.Equipment)
                {
                    inventory.Equipment[slot] = new Dictionary<MongoId, double>(pool);
                }

                foreach (var (slot, chance) in loadout.Chances)
                {
                    chances.EquipmentChances[slot] = chance;
                }

                // The loot cache key of a role is its name, of a variant its name and difficulty
                var lootKey = variant is null ? role.ToLowerInvariant() : $"{role.ToLowerInvariant()}/{difficulty}";
                foreach (var (item, chance) in loadout.GuaranteedBackpackLoot)
                {
                    // A guaranteed item has to be in the pool it is taken from
                    inventory.Items.Backpack[item] = 1;
                    if (!botConfig.GuaranteedLoot.TryGetValue(lootKey, out var guaranteed))
                    {
                        guaranteed = [];
                        botConfig.GuaranteedLoot[lootKey] = guaranteed;
                    }

                    guaranteed[item] = chance;
                }
            }
        }
    }

    /// <summary>
    ///     Read in data from seasonalEvents.json and add found loot items to bots
    /// </summary>
    /// <param name="eventType">Name of the event to read loot in from config</param>
    public void AddEventLootToBots(SeasonalEventType eventType)
    {
        var botLootChanges = seasonalEventConfig.EventLoot.GetValueOrDefault(eventType, null);
        if (botLootChanges is null)
        {
            logger.Warning(serverLocalisationService.GetText("gameevent-no_gear_data", eventType));

            return;
        }

        // Iterate over bots with changes to apply
        foreach (var botKvpP in botLootChanges)
        {
            var botToUpdate = botTable.Types[botKvpP.Key.ToLowerInvariant()];
            if (botToUpdate is null)
            {
                logger.Warning(serverLocalisationService.GetText("gameevent-bot_not_found", botKvpP));
                continue;
            }

            // Iterate over each loot slot change
            var lootAmendmentsBySlot = botLootChanges[botKvpP.Key];
            foreach (var slotKvP in lootAmendmentsBySlot)
            {
                // Grab loot to add and loop over it
                var itemTplsToAdd = slotKvP.Value;
                foreach (var itemKvP in itemTplsToAdd)
                {
                    var dict = botToUpdate.BotInventory.Items.GetAllPropertiesAsDictionary();
                    dict[itemKvP.Key] = itemTplsToAdd[itemKvP.Key];
                }
            }
        }
    }

    /// <summary>
    ///     Add santa to maps
    /// </summary>
    public void AddGifterBotToMaps()
    {
        var gifterSettings = seasonalEventConfig.GifterSettings;
        var maps = locationTable.GetDictionary();
        foreach (var gifterMapSettings in gifterSettings)
        {
            if (!maps.TryGetValue(locationTable.GetMappedKey(gifterMapSettings.Map), out var mapData))
            {
                logger.Warning($"AddGifterBotToMaps() Map not found {gifterMapSettings.Map}");

                continue;
            }

            // Don't add gifter to map twice
            var existingGifter = mapData.Base.BossLocationSpawn.FirstOrDefault(boss => boss.BossName == "gifter");
            if (existingGifter is not null)
            {
                existingGifter.BossChance = gifterMapSettings.SpawnChance;

                continue;
            }

            mapData.Base.BossLocationSpawn.Add(
                new BossLocationSpawn
                {
                    BossName = "gifter",
                    BossChance = gifterMapSettings.SpawnChance,
                    BossZone = gifterMapSettings.Zones,
                    IsBossPlayer = false,
                    BossDifficulty = "normal",
                    BossEscortType = "gifter",
                    BossEscortDifficulty = "normal",
                    BossEscortAmount = "0",
                    ForceSpawn = true,
                    SpawnMode = ["regular", "pve"],
                    Time = -1,
                    TriggerId = string.Empty,
                    TriggerName = string.Empty,
                    Delay = 0,
                    IsRandomTimeSpawn = false,
                    IgnoreMaxBots = true,
                }
            );
        }
    }

    public void ChangeBtrToTarColaSkin()
    {
        var btrSettings = globalTable.Configuration.BTRSettings;

        if (btrSettings.MapsConfigs.TryGetValue("Woods", out var woodsBtrSettings))
        {
            woodsBtrSettings.BtrSkin = "Tarcola";
        }

        if (btrSettings.MapsConfigs.TryGetValue("TarkovStreets", out var streetsBtrSettings))
        {
            streetsBtrSettings.BtrSkin = "Tarcola";
        }
    }

    /// <summary>
    ///     Add the event type the client reads from globals, dropping the None placeholder
    /// </summary>
    /// <param name="globalConfig">globals.json config</param>
    /// <param name="eventTypes">Event types to add</param>
    public void AddClientEventTypes(GlobalConfig globalConfig, params EventType[] eventTypes)
    {
        globalConfig.EventType = globalConfig.EventType.Where(x => x != EventType.None).ToList();
        foreach (var eventType in eventTypes)
        {
            globalConfig.EventType.Add(eventType);
        }
    }
}
