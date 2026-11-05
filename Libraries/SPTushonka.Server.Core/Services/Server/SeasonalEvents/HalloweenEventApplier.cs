using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using GlobalConfig = SPTarkov.Server.Core.Models.Spt.Tables.GlobalConfig;

namespace SPTarkov.Server.Core.Services.Server.SeasonalEvents;

[Injectable(InjectionType.Singleton)]
public class HalloweenEventApplier(SeasonalEventEffects effects, SeasonalEventConfig seasonalEventConfig) : ISeasonalEventApplier
{
    public SeasonalEventType Type
    {
        get { return SeasonalEventType.Halloween; }
    }

    public void Apply(SeasonalEvent seasonalEvent, GlobalConfig globalConfig)
    {
        effects.AddClientEventTypes(globalConfig, EventType.Halloween, EventType.HalloweenIllumination);

        effects.AddEventGearToBots(Type);
        effects.ApplyEventLoadouts(Type);

        if (seasonalEvent.Settings?.ZombieSettings?.Enabled ?? false)
        {
            effects.ConfigureZombies(seasonalEvent.Settings.ZombieSettings);
        }

        if (seasonalEvent.Settings?.RemoveEntryRequirement is not null)
        {
            effects.RemoveEntryRequirement(seasonalEvent.Settings.RemoveEntryRequirement);
        }

        var infectionAmounts = seasonalEvent.Settings?.ZombieSettings?.MapInfectionAmount;
        if ((seasonalEvent.Settings?.ReplaceBotHostility ?? false) && infectionAmounts is not null)
        {
            effects.ReplaceBotHostility(
                seasonalEventConfig.HostilitySettingsForEvent.FirstOrDefault(x => x.Key == "zombies").Value,
                effects.GetLocationsWithZombies(infectionAmounts)
            );
        }

        if (seasonalEvent.Settings?.AdjustBotAppearances ?? false)
        {
            effects.AdjustBotAppearanceValues(Type);
        }

        if (seasonalEvent.Settings?.LocationOverrides is not null)
        {
            effects.ApplyLocationOverrides(seasonalEvent.Settings.LocationOverrides);
        }

        if (seasonalEvent.Settings?.TraderAvatars is not null)
        {
            effects.ApplyTraderAvatars(seasonalEvent.Settings.TraderAvatars);
        }
    }
}
