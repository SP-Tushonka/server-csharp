using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using GlobalConfig = SPTarkov.Server.Core.Models.Spt.Tables.GlobalConfig;

namespace SPTarkov.Server.Core.Services.Server.SeasonalEvents;

/// <summary>
///     Bots dancing around the christmas tree. It stays off until its event entry is enabled.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class KhorovodEventApplier(LocationTable locationTable) : ISeasonalEventApplier
{
    public SeasonalEventType Type
    {
        get { return SeasonalEventType.Khorovod; }
    }

    public void Apply(SeasonalEvent seasonalEvent, GlobalConfig globalConfig)
    {
        foreach (var (_, location) in locationTable.GetDictionary())
        {
            // Only maps with a tree carry Khorovod settings
            if (location.Base?.Events?.Khorovod?.Chance is null)
            {
                continue;
            }

            location.Base.Events.Khorovod.Chance = 100;
            location.Base.BotLocationModifier.KhorovodChance = 100;
        }
    }
}
