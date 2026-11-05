using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using GlobalConfig = SPTarkov.Server.Core.Models.Spt.Tables.GlobalConfig;

namespace SPTarkov.Server.Core.Services.Server.SeasonalEvents;

/// <summary>
///     The Scourge boss on Labs. It has no client event type. The bosses in the location data are all of it.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ScourgeEventApplier(SeasonalEventEffects effects) : ISeasonalEventApplier
{
    public SeasonalEventType Type
    {
        get { return SeasonalEventType.Scourge; }
    }

    public void Apply(SeasonalEvent seasonalEvent, GlobalConfig globalConfig)
    {
        effects.AddEventBossesToMaps("scourge");
    }
}
