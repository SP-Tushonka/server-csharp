using LootDumpProcessor.Model.Config;
using LootDumpProcessor.Process;
using LootDumpProcessor.Serializers.Json;
using LootDumpProcessor.Serializers.Yaml;
using SPTarkov.Server.Core.Models.Common;

namespace LootDumpProcessor;

public static class LootDumpProcessorContext
{
    // All config files ship alongside the build and are read from its base directory.
    private static readonly string ConfigDirectory = Path.Combine(AppContext.BaseDirectory, "Config");

    // Working directories, all rooted at the build's base directory.
    public static readonly string DumpInputDirectory = Path.Combine(AppContext.BaseDirectory, "Dumps", "Input");
    public static readonly string CollectorTempDirectory = Path.Combine(AppContext.BaseDirectory, "Dumps", "Temp");
    public static readonly string OutputDirectory = Path.Combine(AppContext.BaseDirectory, "Dumps", "Output");
    public static readonly string StorageCacheDirectory = Path.Combine(AppContext.BaseDirectory, "Dumps", "Cache");

    private static Config? _config;
    private static readonly object _configLock = new();
    private static ForcedStatic? _forcedStatic;
    private static readonly object _forcedStaticLock = new();
    private static Dictionary<string, MapDirectoryMapping>? _mapDirectoryMappings;
    private static readonly object _mapDirectoryMappingsLock = new();
    private static HashSet<MongoId>? _staticWeaponIds;
    private static readonly object _staticWeaponIdsLock = new();
    private static Dictionary<string, List<ForcedStaticEntry>>? _forcedItems;
    private static readonly object _forcedItemsLock = new();
    private static Dictionary<string, HashSet<string>>? _forcedLoose;
    private static readonly object _forcedLooseLock = new();
    private static TarkovItems? _tarkovItems;
    private static readonly object _tarkovItemsLock = new();

    public static Config GetConfig()
    {
        lock (_configLock)
        {
            if (_config == null)
            {
                _config = new NetJsonSerializer().Deserialize<Config>(File.ReadAllText(Path.Combine(ConfigDirectory, "config.json")));
            }
        }

        return _config;
    }

    public static ForcedStatic GetForcedStatic()
    {
        lock (_forcedStaticLock)
        {
            if (_forcedStatic == null)
            {
                _forcedStatic = YamlSerializerFactory
                    .GetInstance()
                    .Deserialize<ForcedStatic>(File.ReadAllText(Path.Combine(ConfigDirectory, "forced_static.yaml")));
            }
        }

        return _forcedStatic;
    }

    /// <summary>
    /// Not Used
    /// </summary>
    /// <returns></returns>
    public static Dictionary<string, MapDirectoryMapping> GetDirectoryMappings()
    {
        lock (_mapDirectoryMappingsLock)
        {
            if (_mapDirectoryMappings == null)
            {
                _mapDirectoryMappings = YamlSerializerFactory
                    .GetInstance()
                    .Deserialize<Dictionary<string, MapDirectoryMapping>>(
                        File.ReadAllText(Path.Combine(ConfigDirectory, "map_directory_mapping.yaml"))
                    );
            }
        }

        return _mapDirectoryMappings;
    }

    public static HashSet<MongoId> GetStaticWeaponIds()
    {
        lock (_staticWeaponIdsLock)
        {
            if (_staticWeaponIds == null)
            {
                _staticWeaponIds = GetForcedStatic().StaticWeaponIds.Select(id => new MongoId(id)).ToHashSet();
            }
        }

        return _staticWeaponIds;
    }

    public static Dictionary<string, List<ForcedStaticEntry>> GetForcedItems()
    {
        lock (_forcedItemsLock)
        {
            if (_forcedItems == null)
            {
                _forcedItems = GetForcedStatic().ForcedItems;
            }
        }

        return _forcedItems;
    }

    public static Dictionary<string, HashSet<string>> GetForcedLooseItems()
    {
        lock (_forcedLooseLock)
        {
            if (_forcedLoose == null)
            {
                _forcedLoose = YamlSerializerFactory
                    .GetInstance()
                    .Deserialize<Dictionary<string, HashSet<string>>>(File.ReadAllText(Path.Combine(ConfigDirectory, "forced_loose.yaml")));
            }
        }

        return _forcedLoose;
    }

    public static TarkovItems GetTarkovItems()
    {
        lock (_tarkovItemsLock)
        {
            if (_tarkovItems == null)
            {
                _tarkovItems = new TarkovItems($"{GetConfig().ServerItemsJsonLocation}");
            }
        }

        return _tarkovItems;
    }
}
