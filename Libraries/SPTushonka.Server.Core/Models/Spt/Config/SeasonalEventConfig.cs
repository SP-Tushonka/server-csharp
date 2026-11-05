using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Spt.Config;

public record SeasonalEventConfig : BaseConfig
{
    [JsonPropertyName("kind")]
    public override string Kind { get; set; } = "spt-seasonalevents";

    [JsonPropertyName("enableSeasonalEventDetection")]
    public bool EnableSeasonalEventDetection { get; set; }

    /// <summary>
    ///     event / botType / equipSlot / itemid
    /// </summary>
    [JsonPropertyName("eventGear")]
    public required Dictionary<SeasonalEventType, Dictionary<string, Dictionary<string, Dictionary<MongoId, int>>>> EventGear { get; set; }

    /// <summary>
    ///     event / bot type / equipSlot / itemid
    /// </summary>
    /// <summary>
    ///     What a role wears while an event runs in place of its usual pools, by event, role and difficulty
    /// </summary>
    [JsonPropertyName("eventLoadouts")]
    public Dictionary<SeasonalEventType, Dictionary<string, Dictionary<string, EventBotLoadout>>>? EventLoadouts { get; set; }

    [JsonPropertyName("eventLoot")]
    public required Dictionary<SeasonalEventType, Dictionary<string, Dictionary<string, Dictionary<MongoId, int>>>> EventLoot { get; set; }

    [JsonPropertyName("events")]
    public required List<SeasonalEvent> Events { get; set; }

    [JsonPropertyName("eventBotMapping")]
    public required Dictionary<string, string> EventBotMapping { get; set; }

    [JsonPropertyName("eventBossSpawns")]
    public required Dictionary<string, Dictionary<string, List<BossLocationSpawn>>> EventBossSpawns { get; set; }

    [JsonPropertyName("eventWaves")]
    public required Dictionary<string, Dictionary<string, List<Wave>>> EventWaves { get; set; }

    [JsonPropertyName("gifterSettings")]
    public required List<GifterSetting> GifterSettings { get; set; }

    /// <summary>
    ///     key = event, second key = map name
    /// </summary>
    [JsonPropertyName("hostilitySettingsForEvent")]
    public required Dictionary<string, Dictionary<string, List<AdditionalHostilitySettings>>> HostilitySettingsForEvent { get; set; }

    [JsonPropertyName("khorovodEventTransitWhitelist")]
    public required Dictionary<string, List<int>> KhorovodEventTransitWhitelist { get; set; }

    /// <summary>
    ///     Ids of containers on locations that only have Christmas loot
    /// </summary>
    [JsonPropertyName("christmasContainerIds")]
    public required HashSet<string> ChristmasContainerIds { get; set; }

    /// <summary>
    ///     Season - botType - location (body/feet/hands/head)
    /// </summary>
    [JsonPropertyName("botAppearanceChanges")]
    public required Dictionary<
        SeasonalEventType,
        Dictionary<string, Dictionary<string, Dictionary<MongoId, int>>>
    > BotAppearanceChanges { get; set; }
}

public record SeasonalEvent
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    ///     Run the event whatever the date, for players who want it outside its dates
    /// </summary>
    [JsonPropertyName("alwaysActive")]
    public bool AlwaysActive { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public SeasonalEventType Type { get; set; }

    [JsonPropertyName("startDay")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public int StartDay { get; set; }

    [JsonPropertyName("startMonth")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public int StartMonth { get; set; }

    [JsonPropertyName("endDay")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public int EndDay { get; set; }

    [JsonPropertyName("endMonth")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public int EndMonth { get; set; }

    [JsonPropertyName("settings")]
    public SeasonalEventSettings? Settings { get; set; }

    [JsonPropertyName("setting")]
    public SeasonalEventSettings? SettingsDoNOTUse
    {
        set { Settings = value; }
    }
}

public record SeasonalEventSettings
{
    [JsonPropertyName("enableHalloweenHideout")]
    public bool? EnableHalloweenHideout { get; set; }

    [JsonPropertyName("enableChristmasHideout")]
    public bool? EnableChristmasHideout { get; set; }

    [JsonPropertyName("enableSanta")]
    public bool? EnableSanta { get; set; }

    [JsonPropertyName("adjustBotAppearances")]
    public bool? AdjustBotAppearances { get; set; }

    [JsonPropertyName("addEventGearToBots")]
    public bool? AddEventGearToBots { get; set; }

    [JsonPropertyName("addEventLootToBots")]
    public bool? AddEventLootToBots { get; set; }

    [JsonPropertyName("removeEntryRequirement")]
    public List<string>? RemoveEntryRequirement { get; set; }

    [JsonPropertyName("replaceBotHostility")]
    public bool? ReplaceBotHostility { get; set; }

    [JsonPropertyName("forceSeason")]
    public Season? ForceSeason { get; set; }

    [JsonPropertyName("zombieSettings")]
    public ZombieSettings? ZombieSettings { get; set; }

    [JsonPropertyName("disableBosses")]
    public List<string>? DisableBosses { get; set; }

    [JsonPropertyName("disableWaves")]
    public List<string>? DisableWaves { get; set; }

    [JsonPropertyName("christmasLootBoostAmount")]
    public double? ChristmasLootBoostAmount { get; set; }

    /// <summary>
    ///     Avatar path by trader id while the event runs
    /// </summary>
    [JsonPropertyName("traderAvatars")]
    public Dictionary<MongoId, string>? TraderAvatars { get; set; }

    /// <summary>
    ///     What the event changes on individual maps, by location id
    /// </summary>
    [JsonPropertyName("locationOverrides")]
    public Dictionary<string, EventLocationOverride>? LocationOverrides { get; set; }
}

public record EventBotLoadout
{
    /// <summary>
    ///     Replaces these equipment pools
    /// </summary>
    [JsonPropertyName("equipment")]
    public Dictionary<EquipmentSlots, Dictionary<MongoId, double>> Equipment { get; set; } = [];

    /// <summary>
    ///     Replaces the spawn chance of these equipment slots
    /// </summary>
    [JsonPropertyName("chances")]
    public Dictionary<string, double> Chances { get; set; } = [];

    /// <summary>
    ///     Chance percent of a backpack holding these items, on top of its random loot
    /// </summary>
    [JsonPropertyName("guaranteedBackpackLoot")]
    public Dictionary<MongoId, double> GuaranteedBackpackLoot { get; set; } = [];
}

public record EventLocationOverride
{
    /// <summary>
    ///     Replaces the map's hostility list
    /// </summary>
    [JsonPropertyName("additionalHostilitySettings")]
    public List<AdditionalHostilitySettings>? AdditionalHostilitySettings { get; set; }

    /// <summary>
    ///     Added to the map's item caps
    /// </summary>
    [JsonPropertyName("itemCaps")]
    public List<MaxItemCountInLocation>? ItemCaps { get; set; }

    /// <summary>
    ///     Bosses that do not spawn on the map while the event runs
    /// </summary>
    [JsonPropertyName("zeroChanceBosses")]
    public List<string>? ZeroChanceBosses { get; set; }
}

public record ZombieSettings
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("mapInfectionAmount")]
    public Dictionary<string, double>? MapInfectionAmount { get; set; }

    [JsonPropertyName("disableBosses")]
    public List<string>? DisableBosses { get; set; }

    [JsonPropertyName("disableWaves")]
    public List<string>? DisableWaves { get; set; }

    /// <summary>
    ///     Bosses whose spawn chance the infection does not lower
    /// </summary>
    [JsonPropertyName("unscaledBosses")]
    public List<string>? UnscaledBosses { get; set; }
}

public record GifterSetting
{
    [JsonPropertyName("map")]
    public string? Map { get; set; }

    [JsonPropertyName("zones")]
    public string? Zones { get; set; }

    [JsonPropertyName("spawnChance")]
    public int? SpawnChance { get; set; }
}
