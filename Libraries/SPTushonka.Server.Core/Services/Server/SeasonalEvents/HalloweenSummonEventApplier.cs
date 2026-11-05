using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using GlobalConfig = SPTarkov.Server.Core.Models.Spt.Tables.GlobalConfig;

namespace SPTarkov.Server.Core.Services.Server.SeasonalEvents;

/// <summary>
///     The Zryachiy summoning ritual from an earlier Halloween. It stays off until its event entry is enabled.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class HalloweenSummonEventApplier(
    SeasonalEventEffects effects,
    GlobalTable globalTable,
    BotTable botTable,
    SeasonalEventConfig seasonalEventConfig
) : ISeasonalEventApplier
{
    public SeasonalEventType Type
    {
        get { return SeasonalEventType.HalloweenSummon; }
    }

    public void Apply(SeasonalEvent seasonalEvent, GlobalConfig globalConfig)
    {
        globalTable.Configuration.EventSettings.EventActive = true;

        if (seasonalEventConfig.HostilitySettingsForEvent.TryGetValue("summon", out var hostility))
        {
            effects.ReplaceBotHostility(hostility);
        }

        effects.AddEventBossesToMaps("halloweensummon");
        ForceZryachiyMelee();
    }

    /// <summary>
    ///     Give Zryachiy the melee weapon the ritual needs
    /// </summary>
    private void ForceZryachiyMelee()
    {
        if (botTable.Types.TryGetValue("bosszryachiy", out var zryachiy) && zryachiy is not null)
        {
            zryachiy.BotChances.EquipmentChances["Scabbard"] = 100;
        }
    }
}
