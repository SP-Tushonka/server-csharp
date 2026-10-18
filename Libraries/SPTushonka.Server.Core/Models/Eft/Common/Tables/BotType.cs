using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot.GlobalSettings;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

public record BotType
{
    [JsonPropertyName("appearance")]
    public Appearance BotAppearance { get; set; }

    [JsonPropertyName("chances")]
    public Chances BotChances { get; set; }

    [JsonPropertyName("difficulty")]
    public Dictionary<string, DifficultyCategories> BotDifficulty { get; set; }

    [JsonPropertyName("experience")]
    public Experience BotExperience { get; set; }

    [JsonPropertyName("firstName")]
    public List<string> FirstNames { get; set; }

    [JsonPropertyName("generation")]
    public Generation BotGeneration { get; set; }

    [JsonPropertyName("health")]
    public BotTypeHealth BotHealth { get; set; }

    [JsonPropertyName("inventory")]
    public BotTypeInventory BotInventory { get; set; }

    [JsonPropertyName("lastName")]
    public IEnumerable<string> LastNames { get; set; }

    [JsonPropertyName("skills")]
    public BotDbSkills BotSkills { get; set; }

    /// <summary>
    ///     Loadouts used in place of the role's own when it is generated at a given difficulty, keyed by difficulty.
    ///     Some bosses share their role with their guards and differ only by difficulty
    /// </summary>
    [JsonPropertyName("difficultyVariants")]
    public Dictionary<string, BotTypeVariant>? DifficultyVariants { get; set; }
}

public record BotTypeVariant
{
    [JsonPropertyName("appearance")]
    public Appearance BotAppearance { get; set; }

    [JsonPropertyName("chances")]
    public Chances BotChances { get; set; }

    [JsonPropertyName("firstName")]
    public List<string> FirstNames { get; set; }

    [JsonPropertyName("generation")]
    public Generation BotGeneration { get; set; }

    [JsonPropertyName("inventory")]
    public BotTypeInventory BotInventory { get; set; }
}

public record Appearance
{
    [JsonPropertyName("body")]
    public Dictionary<MongoId, double> Body { get; set; }

    [JsonPropertyName("feet")]
    public Dictionary<MongoId, double> Feet { get; set; }

    [JsonPropertyName("hands")]
    [JsonConverter(typeof(ArrayToObjectFactoryConverter))]
    public Dictionary<MongoId, double> Hands { get; set; }

    [JsonPropertyName("head")]
    [JsonConverter(typeof(ArrayToObjectFactoryConverter))]
    public Dictionary<MongoId, double> Head { get; set; }

    [JsonPropertyName("voice")]
    [JsonConverter(typeof(ArrayToObjectFactoryConverter))]
    public Dictionary<MongoId, double> Voice { get; set; }
}

public record Chances
{
    [JsonPropertyName("equipment")]
    public Dictionary<string, double> EquipmentChances { get; set; }

    [JsonPropertyName("weaponMods")]
    public Dictionary<string, double> WeaponModsChances { get; set; }

    [JsonPropertyName("equipmentMods")]
    public Dictionary<string, double> EquipmentModsChances { get; set; }
}

/// <summary>
/// See BotSettingsComponents in the client, this record should match that
/// </summary>
public record DifficultyCategories
{
    public required BotGlobalAimingSettings Aiming { get; set; }

    public required BotGlobalsBossSettings Boss { get; set; }

    public required BotGlobalsChangeSettings Change { get; set; }

    public required BotGlobalCoreSettings Core { get; set; }

    public required BotGlobalsCoverSettings Cover { get; set; }

    public required BotGlobalsGrenadeSettings Grenade { get; set; }

    public required BotGlobalsHearingSettings Hearing { get; set; }

    public required BotGlobalLayData Lay { get; set; }

    public required BotGlobalLookData Look { get; set; }

    public required BotGlobalsMindSettings Mind { get; set; }

    public required BotGlobalsMoveSettings Move { get; set; }

    public required BotGlobalPatrolSettings Patrol { get; set; }

    public required BotGlobalsScatteringSettings Scattering { get; set; }

    public required BotGlobalShootData Shoot { get; set; }
}

public record Experience
{
    /// <summary>
    ///     key = bot difficulty
    /// </summary>
    [JsonPropertyName("aggressorBonus")]
    public Dictionary<string, double> AggressorBonus { get; set; }

    [JsonPropertyName("level")]
    public MinMax<int> Level { get; set; }

    /// <summary>
    ///     key = bot difficulty
    /// </summary>
    [JsonPropertyName("reward")]
    public Dictionary<string, MinMax<int>> Reward { get; set; }

    /// <summary>
    ///     key = bot difficulty
    /// </summary>
    [JsonPropertyName("standingForKill")]
    public Dictionary<string, double> StandingForKill { get; set; }

    [JsonPropertyName("useSimpleAnimator")]
    public bool UseSimpleAnimator { get; set; }
}

public record Generation
{
    [JsonPropertyName("items")]
    public GenerationWeightingItems Items { get; set; }
}

public record GenerationData
{
    /// <summary>
    ///     key: number of items, value: weighting
    /// </summary>
    [JsonPropertyName("weights")]
    public Dictionary<double, double> Weights { get; set; }

    /// <summary>
    ///     Array of item tpls
    /// </summary>
    [JsonPropertyName("whitelist")]
    [JsonConverter(typeof(ArrayToObjectFactoryConverter))]
    public Dictionary<MongoId, double> Whitelist { get; set; }
}

public record GenerationWeightingItems
{
    [JsonPropertyName("grenades")]
    public GenerationData Grenades { get; set; }

    [JsonPropertyName("healing")]
    public GenerationData Healing { get; set; }

    [JsonPropertyName("drugs")]
    public GenerationData Drugs { get; set; }

    [JsonPropertyName("food")]
    public GenerationData Food { get; set; }

    [JsonPropertyName("drink")]
    public GenerationData Drink { get; set; }

    [JsonPropertyName("currency")]
    public GenerationData Currency { get; set; }

    [JsonPropertyName("stims")]
    public GenerationData Stims { get; set; }

    [JsonPropertyName("backpackLoot")]
    public GenerationData BackpackLoot { get; set; }

    [JsonPropertyName("pocketLoot")]
    public GenerationData PocketLoot { get; set; }

    [JsonPropertyName("vestLoot")]
    public GenerationData VestLoot { get; set; }

    [JsonPropertyName("magazines")]
    public GenerationData Magazines { get; set; }

    [JsonPropertyName("specialItems")]
    public GenerationData SpecialItems { get; set; }

    [JsonPropertyName("looseLoot")]
    public GenerationData LooseLoot { get; set; }

    /// <summary>
    ///     Items other than ammo in the secure container, when absent the container is filled from its pool
    /// </summary>
    [JsonPropertyName("secureLoot")]
    public GenerationData? SecureLoot { get; set; }

    /// <summary>
    ///     Full ammo stacks the secure container holds for each weapon, keyed by weapon slot.
    ///     A slot without an entry uses the bot config stack count
    /// </summary>
    [JsonPropertyName("secureAmmoStacks")]
    public Dictionary<string, GenerationData>? SecureAmmoStacks { get; set; }

    /// <summary>
    ///     Whether a magazine fed weapon has a round chambered, weights for 0 and 1 keyed by weapon slot.
    ///     A slot without an entry is always chambered
    /// </summary>
    [JsonPropertyName("chamberedRounds")]
    public Dictionary<string, GenerationData>? ChamberedRounds { get; set; }

    /// <summary>
    ///     Share of each kind of item held per container, keyed by loot category or by weapon slot for its spare magazines.
    ///     A kind without an entry uses the default order
    /// </summary>
    [JsonPropertyName("placement")]
    public Dictionary<string, Dictionary<string, double>>? Placement { get; set; }
}

public record BotTypeHealth
{
    public IEnumerable<BodyPart> BodyParts { get; set; }

    public MinMax<double> Energy { get; set; }

    public MinMax<double> Hydration { get; set; }

    public MinMax<double> Temperature { get; set; }
}

public record BodyPart
{
    public MinMax<double> Chest { get; set; }

    public MinMax<double> Head { get; set; }

    public MinMax<double> LeftArm { get; set; }

    public MinMax<double> LeftLeg { get; set; }

    public MinMax<double> RightArm { get; set; }

    public MinMax<double> RightLeg { get; set; }

    public MinMax<double> Stomach { get; set; }
}

public record BotTypeInventory
{
    [JsonPropertyName("equipment")]
    public Dictionary<EquipmentSlots, Dictionary<MongoId, double>> Equipment { get; set; }

    public Dictionary<string, Dictionary<MongoId, double>> Ammo { get; set; }

    [JsonPropertyName("items")]
    public ItemPools Items { get; set; }

    [JsonPropertyName("mods")]
    public GlobalMods Mods { get; set; }

    /// <summary>
    ///     Complete weapon builds keyed by weapon template, for roles that only ever carry a few fixed builds.
    ///     A weapon listed here is assembled from one of its builds instead of slot by slot from the mod pool
    /// </summary>
    [JsonPropertyName("weaponBuilds")]
    public Dictionary<MongoId, List<BotItemBuild>>? WeaponBuilds { get; set; }

    /// <summary>
    ///     Complete gear builds keyed by item template, the attachments and plates a helmet, armor or rig is seen with.
    ///     A gear item listed here gets one of its builds instead of attachments rolled from the mod pool
    /// </summary>
    [JsonPropertyName("equipmentBuilds")]
    public Dictionary<MongoId, List<BotItemBuild>>? EquipmentBuilds { get; set; }
}

/// <summary>
///     One complete item build, the root item followed by its attachments without ammo
/// </summary>
public record BotItemBuild
{
    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("items")]
    public List<Item> Items { get; set; }
}

public record ItemPools
{
    public Dictionary<MongoId, double> Backpack { get; set; }

    public Dictionary<MongoId, double> Pockets { get; set; }

    public Dictionary<MongoId, double> SecuredContainer { get; set; }

    public Dictionary<MongoId, double> SpecialLoot { get; set; }

    public Dictionary<MongoId, double> TacticalVest { get; set; }
}

public record BotDbSkills
{
    public Dictionary<string, MinMax<double>> Common { get; set; }

    public Dictionary<string, MinMax<double>>? Mastering { get; set; }
}
