using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using SPTarkov.Common.Extensions;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Server.SeasonalEvents;
using SPTarkov.Server.Core.Utils;
using GlobalConfig = SPTarkov.Server.Core.Models.Spt.Tables.GlobalConfig;

namespace SPTarkov.Server.Core.Services.Server;

[Injectable(InjectionType.Singleton)]
public class SeasonalEventService(
    ISptLogger<SeasonalEventService> logger,
    GlobalTable globalTable,
    BotTable botTable,
    TimeUtil timeUtil,
    GiftService giftService,
    ServerLocalisationService serverLocalisationService,
    ProfileHelper profileHelper,
    QuestConfig questConfig,
    SeasonalEventConfig seasonalEventConfig,
    WeatherConfig weatherConfig,
    SeasonalEventEffects effects,
    IEnumerable<ISeasonalEventApplier> eventAppliers
)
{
    /// <summary>Events applied this load, the source of truth for the per event enabled checks</summary>
    private readonly HashSet<SeasonalEventType> _activeEventTypes = [];

    protected readonly FrozenSet<MongoId> ChristmasEventItems =
    [
        ItemTpl.ARMOR_6B13_M_ASSAULT_ARMOR_CHRISTMAS_EDITION,
        ItemTpl.BACKPACK_SANTAS_BAG,
        ItemTpl.BARTER_CHRISTMAS_TREE_ORNAMENT_RED,
        ItemTpl.BARTER_CHRISTMAS_TREE_ORNAMENT_SILVER,
        ItemTpl.BARTER_CHRISTMAS_TREE_ORNAMENT_VIOLET,
        ItemTpl.BARTER_JAR_OF_PICKLES,
        ItemTpl.FOOD_OLIVIER_SALAD_BOX,
        ItemTpl.BARTER_SPECIAL_40DEGREE_FUEL,
        ItemTpl.HEADWEAR_DED_MOROZ_HAT,
        ItemTpl.HEADWEAR_ELF_HAT,
        ItemTpl.HEADWEAR_HAT_WITH_ANTLERS,
        ItemTpl.HEADWEAR_MASKA1SCH_BULLETPROOF_HELMET_CHRISTMAS_EDITION,
        ItemTpl.HEADWEAR_SANTA_HAT,
        ItemTpl.RANDOMLOOTCONTAINER_NEW_YEAR_GIFT_BIG,
        ItemTpl.RANDOMLOOTCONTAINER_NEW_YEAR_GIFT_MEDIUM,
        ItemTpl.RANDOMLOOTCONTAINER_NEW_YEAR_GIFT_SMALL,
        ItemTpl.FACECOVER_ASTRONOMER_MASK,
        ItemTpl.FACECOVER_CIPOLLINO_MASK,
        ItemTpl.FACECOVER_FAKE_WHITE_BEARD,
        ItemTpl.FACECOVER_FOX_MASK,
        ItemTpl.FACECOVER_GRINCH_MASK,
        ItemTpl.FACECOVER_HARE_MASK,
        ItemTpl.FACECOVER_ROOSTER_MASK,
        ItemTpl.FLARE_RSP30_REACTIVE_SIGNAL_CARTRIDGE_FIREWORK,
        ItemTpl.AMMO_26X75_SIGNAL_FLARE_NEW_YEAR,
        ItemTpl.BARTER_SHYSHKA_CHRISTMAS_TREE_LIFE_EXTENDER,
        ItemTpl.BACKPACK_MYSTERY_RANCH_TERRAFRAME_BACKPACK_CHRISTMAS_EDITION,
    ];

    private List<SeasonalEvent> _currentlyActiveEvents = [];

    protected readonly FrozenSet<EquipmentSlots> EquipmentSlotsToFilter =
    [
        EquipmentSlots.FaceCover,
        EquipmentSlots.Headwear,
        EquipmentSlots.Backpack,
        EquipmentSlots.TacticalVest,
    ];

    protected readonly FrozenSet<MongoId> HalloweenEventItems =
    [
        ItemTpl.HEADWEAR_JACKOLANTERN_TACTICAL_PUMPKIN_HELMET,
        ItemTpl.FACECOVER_FACELESS_MASK,
        ItemTpl.FACECOVER_GHOUL_MASK,
        ItemTpl.FACECOVER_SPOOKY_SKULL_MASK,
        ItemTpl.FACECOVER_SPOOKY_SKULL_MASK_2,
        ItemTpl.FACECOVER_GHOUL_MASK_2,
        ItemTpl.FACECOVER_JASON_MASK,
        ItemTpl.FACECOVER_MISHA_MAYOROV_MASK,
        ItemTpl.FACECOVER_SLENDER_MASK,
        ItemTpl.FACECOVER_SLENDER_MASK_2,
        ItemTpl.RANDOMLOOTCONTAINER_PUMPKIN_RAND_LOOT_CONTAINER,
    ];

    /// <summary>
    ///     Get an array of christmas items found in bots inventories as loot
    /// </summary>
    /// <returns>array</returns>
    public FrozenSet<MongoId> GetChristmasEventItems()
    {
        return ChristmasEventItems;
    }

    /// <summary>
    ///     Get an array of halloween items found in bots inventories as loot
    /// </summary>
    /// <returns>array</returns>
    public FrozenSet<MongoId> GetHalloweenEventItems()
    {
        return HalloweenEventItems;
    }

    public bool ItemIsChristmasRelated(MongoId itemTpl)
    {
        return ChristmasEventItems.Contains(itemTpl);
    }

    public bool ItemIsHalloweenRelated(MongoId itemTpl)
    {
        return HalloweenEventItems.Contains(itemTpl);
    }

    /// <summary>
    ///     Check if item id exists in christmas or halloween event arrays
    /// </summary>
    /// <param name="itemTpl">item tpl to check for</param>
    /// <returns></returns>
    public bool ItemIsSeasonalRelated(MongoId itemTpl)
    {
        return ChristmasEventItems.Contains(itemTpl) || HalloweenEventItems.Contains(itemTpl);
    }

    /// <summary>
    ///     Get active seasonal events
    /// </summary>
    /// <returns>Array of active events</returns>
    public List<SeasonalEvent> GetActiveEvents()
    {
        return _currentlyActiveEvents;
    }

    /// <summary>
    ///     Get an array of seasonal items that should not appear
    ///     e.g. if halloween is active, only return christmas items
    ///     or, if halloween and christmas are inactive, return both sets of items
    /// </summary>
    /// <returns>array of tpl strings</returns>
    public HashSet<MongoId> GetInactiveSeasonalEventItems()
    {
        var items = new HashSet<MongoId>();
        if (!ChristmasEventEnabled())
        {
            items.UnionWith(ChristmasEventItems);
        }

        if (!HalloweenEventEnabled())
        {
            items.UnionWith(HalloweenEventItems);
        }

        return items;
    }

    /// <summary>
    ///     Is a seasonal event currently active
    /// </summary>
    /// <returns>true if event is active</returns>
    public bool SeasonalEventEnabled()
    {
        return ChristmasEventEnabled() || HalloweenEventEnabled();
    }

    /// <summary>
    ///     Is christmas event active
    /// </summary>
    /// <returns>true if active</returns>
    public bool ChristmasEventEnabled()
    {
        return _activeEventTypes.Contains(SeasonalEventType.Christmas);
    }

    /// <summary>
    ///     is halloween event active
    /// </summary>
    /// <returns>true if active</returns>
    public bool HalloweenEventEnabled()
    {
        return _activeEventTypes.Contains(SeasonalEventType.Halloween);
    }

    /// <summary>
    ///     Is the Scourge event on Labs active, with its boss, quests and quest item spawns
    /// </summary>
    /// <returns>True if active</returns>
    public bool ScourgeEventEnabled()
    {
        return _activeEventTypes.Contains(SeasonalEventType.Scourge);
    }

    /// <summary>
    ///     Is detection of seasonal events enabled (halloween / christmas)
    /// </summary>
    /// <returns>true if seasonal events should be checked for</returns>
    public bool IsAutomaticEventDetectionEnabled()
    {
        return seasonalEventConfig.EnableSeasonalEventDetection;
    }

    /// <summary>
    ///     Get the dates each seasonal event starts and ends at
    /// </summary>
    /// <returns>Record with event name + start/end date</returns>
    public List<SeasonalEvent> GetEventDetails()
    {
        return seasonalEventConfig.Events;
    }

    /// <summary>
    ///     Look up quest in configs/quest.json
    /// </summary>
    /// <param name="questId">Quest to look up</param>
    /// <param name="eventType">event type (Christmas/Halloween/None)</param>
    /// <returns>true if related</returns>
    public bool IsQuestRelatedToEvent(MongoId questId, SeasonalEventType eventType)
    {
        var eventQuestData = questConfig.EventQuests.GetValueOrDefault(questId, null);
        return eventQuestData?.Season == eventType;
    }

    /// <summary>
    ///     Handle activating seasonal events
    /// </summary>
    public void EnableSeasonalEvents()
    {
        if (_currentlyActiveEvents.Any())
        {
            var globalConfig = globalTable.Configuration;
            foreach (var activeEvent in _currentlyActiveEvents)
            {
                UpdateGlobalEvents(globalConfig, activeEvent);
            }
        }
    }

    /// <summary>
    ///     Force a seasonal event to be active
    /// </summary>
    /// <param name="eventType">Event to force active</param>
    /// <returns>True if event was successfully force enabled</returns>
    public bool ForceSeasonalEvent(SeasonalEventType eventType)
    {
        var globalConfig = globalTable.Configuration;
        var seasonEvent = seasonalEventConfig.Events.FirstOrDefault(e => e.Type == eventType);
        if (seasonEvent is null)
        {
            logger.Warning($"Unable to force event: {eventType} as it cannot be found in events config");
            return false;
        }

        UpdateGlobalEvents(globalConfig, seasonEvent);

        return true;
    }

    /// <summary>
    ///     Store active events inside class list property `currentlyActiveEvents` + set class properties: christmasEventActive/halloweenEventActive
    /// </summary>
    public void CacheActiveEvents()
    {
        var currentDate = DateTimeOffset.Now.DateTime;
        var seasonalEvents = GetEventDetails();

        // reset existing data
        _currentlyActiveEvents = [];

        // Add active events to array
        foreach (var events in seasonalEvents)
        {
            if (!events.Enabled)
            {
                continue;
            }

            if (events.AlwaysActive || currentDate.DateIsBetweenTwoDates(events.StartMonth, events.StartDay, events.EndMonth, events.EndDay))
            {
                _currentlyActiveEvents.Add(events);
            }
        }
    }

    /// <summary>
    ///     Get the currently active weather season e.g. SUMMER/AUTUMN/WINTER
    /// </summary>
    /// <returns>Season enum value</returns>
    public Season GetActiveWeatherSeason()
    {
        if (weatherConfig.OverrideSeason.HasValue)
        {
            return weatherConfig.OverrideSeason.Value;
        }

        var currentDate = timeUtil.GetDateTimeNow();
        foreach (var seasonRange in weatherConfig.SeasonDates)
        {
            if (
                currentDate.DateIsBetweenTwoDates(
                    seasonRange.StartMonth ?? 0,
                    seasonRange.StartDay ?? 0,
                    seasonRange.EndMonth ?? 0,
                    seasonRange.EndDay ?? 0
                )
            )
            {
                return seasonRange.SeasonType ?? Season.SUMMER;
            }
        }

        logger.Warning(serverLocalisationService.GetText("season-no_matching_season_found_for_date"));

        return Season.SUMMER;
    }

    /// <summary>
    ///     Iterate through bots inventory and loot to find and remove christmas items (as defined in SeasonalEventService)
    /// </summary>
    /// <param name="botInventory">Bots inventory to iterate over</param>
    /// <param name="botRole">the role of the bot being processed</param>
    public void RemoveChristmasItemsFromBotInventory(BotTypeInventory botInventory, string botRole)
    {
        var christmasItems = GetChristmasEventItems();

        // Remove christmas related equipment
        foreach (var equipmentSlotKey in EquipmentSlotsToFilter)
        {
            if (!botInventory.Equipment.TryGetValue(equipmentSlotKey, out var equipment))
            {
                logger.Warning(
                    serverLocalisationService.GetText(
                        "seasonal-missing_equipment_slot_on_bot",
                        new { equipmentSlot = equipmentSlotKey, botRole }
                    )
                );

                continue;
            }

            botInventory.Equipment[equipmentSlotKey] = equipment.Where(i => !ChristmasEventItems.Contains(i.Key)).ToDictionary();
        }

        var containersToCheck = new List<Dictionary<MongoId, double>>
        {
            botInventory.Items.Backpack,
            botInventory.Items.Pockets,
            botInventory.Items.SecuredContainer,
            botInventory.Items.TacticalVest,
            botInventory.Items.SpecialLoot,
        };

        foreach (var container in containersToCheck)
        {
            // Find all Christmas items in container and remove
            container.RemoveItems(christmasItems);
        }
    }

    /// <summary>
    ///     Make adjusted to server code based on the name of the event passed in
    /// </summary>
    /// <param name="globalConfig">globals.json</param>
    /// <param name="eventType">Name of the event to enable. e.g. Christmas</param>
    protected void UpdateGlobalEvents(GlobalConfig globalConfig, SeasonalEvent eventType)
    {
        logger.Success(serverLocalisationService.GetText("season-event_is_active", eventType.Type));

        // The flag is what gates each event's quests and loot, so it is set whether or not an applier exists.
        // Applying an event twice would stack its changes, so an event that is already on stops here.
        if (!_activeEventTypes.Add(eventType.Type))
        {
            return;
        }

        var applier = eventAppliers.FirstOrDefault(candidate => candidate.Type == eventType.Type);
        if (applier is null)
        {
            // A mod added this one. It can only use the settings every event shares
            HandleModEvent(eventType, globalConfig);

            return;
        }

        applier.Apply(eventType, globalConfig);
    }

    public async Task GivePlayerSeasonalGiftsAsync(MongoId sessionId)
    {
        if (_currentlyActiveEvents is null)
        {
            return;
        }

        foreach (var seasonEvent in _currentlyActiveEvents)
        {
            var applier = eventAppliers.FirstOrDefault(candidate => candidate.Type == seasonEvent.Type);
            foreach (var giftKey in applier?.GiftKeys ?? [])
            {
                await GiveGiftAsync(sessionId, giftKey);
            }
        }
    }

    /// <summary>
    ///     Apply an event no applier claims, which a mod added. Only the settings every event shares are read.
    /// </summary>
    /// <param name="seasonalEvent">Event being enabled</param>
    /// <param name="globalConfig">globals.json config</param>
    protected void HandleModEvent(SeasonalEvent seasonalEvent, GlobalConfig globalConfig)
    {
        if (seasonalEvent.Settings?.EnableChristmasHideout ?? false)
        {
            effects.AddClientEventTypes(globalConfig, EventType.Christmas);
        }

        if (seasonalEvent.Settings?.EnableHalloweenHideout ?? false)
        {
            effects.AddClientEventTypes(globalConfig, EventType.Halloween, EventType.HalloweenIllumination);
        }

        if (seasonalEvent.Settings?.AddEventGearToBots ?? false)
        {
            effects.AddEventGearToBots(seasonalEvent.Type);
        }

        if (seasonalEvent.Settings?.AddEventLootToBots ?? false)
        {
            effects.AddEventLootToBots(seasonalEvent.Type);
        }

        if (seasonalEvent.Settings?.ZombieSettings?.Enabled ?? false)
        {
            effects.ConfigureZombies(seasonalEvent.Settings.ZombieSettings);
        }

        if (seasonalEvent.Settings?.ForceSeason != null)
        {
            weatherConfig.OverrideSeason = seasonalEvent.Settings.ForceSeason;
        }

        if (seasonalEvent.Settings?.AdjustBotAppearances ?? false)
        {
            effects.AdjustBotAppearanceValues(seasonalEvent.Type);
        }
    }

    /// <summary>
    ///     Send gift to player if they have not already received it
    /// </summary>
    /// <param name="playerId">Player to send gift to</param>
    /// <param name="giftKey">Key of gift to give</param>
    protected async Task GiveGiftAsync(MongoId playerId, string giftKey)
    {
        var giftData = giftService.GetGiftById(giftKey);
        if (!profileHelper.PlayerHasReceivedMaxNumberOfGift(playerId, giftKey, giftData.MaxToSendPlayer ?? 5))
        {
            await giftService.SendGiftToPlayerAsync(playerId, giftKey);
        }
    }

    /// <summary>
    ///     Get the underlying bot type for an event bot e.g. `peacefullZryachiyEvent` will return `bossZryachiy`
    /// </summary>
    /// <param name="eventBotRole">Event bot role type</param>
    /// <returns>Bot role as string</returns>
    public string GetBaseRoleForEventBot(string? eventBotRole)
    {
        return seasonalEventConfig.EventBotMapping.GetValueOrDefault(eventBotRole, null);
    }

    /// <summary>
    ///     Force the weather to be snow
    /// </summary>
    public void EnableSnow()
    {
        weatherConfig.OverrideSeason = Season.WINTER;
    }
}
