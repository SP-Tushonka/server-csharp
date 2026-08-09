namespace SPTarkov.Server.Core.Models.Enums;

public enum ModSpawn
{
    /// <summary>
    /// Chosen mod should be the tpl from the default weapon template
    /// </summary>
    DEFAULT_MOD = 0,

    /// <summary>
    /// Normal behaviour
    /// </summary>
    SPAWN = 1,

    /// <summary>
    /// Item should not be chosen
    /// </summary>
    SKIP = 2,
}
