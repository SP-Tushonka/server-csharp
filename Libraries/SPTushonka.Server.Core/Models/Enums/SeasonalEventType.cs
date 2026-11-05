namespace SPTarkov.Server.Core.Models.Enums;

public enum SeasonalEventType
{
    None,
    Christmas,
    Halloween,
    Scourge,

    /// <summary>Gift tag for anything not tied to a seasonal event, never an event in its own right</summary>
    Promo,

    /// <summary>Zryachiy summoning ritual from an earlier Halloween, off unless enabled in config</summary>
    HalloweenSummon,

    /// <summary>Bots dancing around the christmas tree, off unless enabled in config</summary>
    Khorovod,

    /// <summary>Christmas tree patrol event and its transit, off unless enabled in config</summary>
    Runddans,
}
