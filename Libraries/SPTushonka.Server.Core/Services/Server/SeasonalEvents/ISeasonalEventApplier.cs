using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using GlobalConfig = SPTarkov.Server.Core.Models.Spt.Tables.GlobalConfig;

namespace SPTarkov.Server.Core.Services.Server.SeasonalEvents;

/// <summary>
///     One seasonal event's effect on the loaded database. Implementations are resolved as a set and the
///     matching one is applied for each active event, so adding an event needs no change here.
/// </summary>
public interface ISeasonalEventApplier
{
    /// <summary>
    ///     Event this applies, matched against the type in seasonalevents.json
    /// </summary>
    SeasonalEventType Type { get; }

    /// <summary>
    ///     Apply the event to the database
    /// </summary>
    /// <param name="seasonalEvent">Event being enabled, carrying its settings</param>
    /// <param name="globalConfig">globals.json config the client reads</param>
    void Apply(SeasonalEvent seasonalEvent, GlobalConfig globalConfig);

    /// <summary>
    ///     Gift keys handed to a player while this event runs
    /// </summary>
    IEnumerable<string> GiftKeys
    {
        get { return []; }
    }
}
