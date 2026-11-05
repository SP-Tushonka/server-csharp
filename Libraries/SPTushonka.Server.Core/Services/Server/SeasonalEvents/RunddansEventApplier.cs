using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using GlobalConfig = SPTarkov.Server.Core.Models.Spt.Tables.GlobalConfig;

namespace SPTarkov.Server.Core.Services.Server.SeasonalEvents;

/// <summary>
///     The christmas tree patrol event and its transit whitelist. It stays off until its event entry is enabled.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class RunddansEventApplier(GlobalTable globalTable, BotTable botTable) : ISeasonalEventApplier
{
    public SeasonalEventType Type
    {
        get { return SeasonalEventType.Runddans; }
    }

    public void Apply(SeasonalEvent seasonalEvent, GlobalConfig globalConfig)
    {
        globalTable.Configuration.RunddansSettings.Active = true;
        globalTable.Configuration.RunddansSettings.ActivePVE = true;
        botTable.Core.ActivePatrolGeneratorEvent = true;
    }
}
