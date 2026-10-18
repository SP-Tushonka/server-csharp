using Common.Models;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using BotType = Common.Models.BotType;

namespace Generator.Helpers;

/// <summary>
///     Roles that carry a different loadout at some difficulties, such as a boss sharing its role with its guards.
///     Those difficulties are learned apart and written into the role's difficultyVariants
/// </summary>
public static class RoleVariants
{
    private static readonly Dictionary<BotType, string[]> _variantDifficulties = new()
    {
        [BotType.bosswedge] = ["easy"],
        [BotType.assault] = ["easy", "hard", "impossible"],
        [BotType.marksman] = ["easy", "hard", "impossible"],
    };

    /// <summary>
    ///     Create an empty bot for every difficulty variant of the roles being generated
    /// </summary>
    /// <param name="botTypes">Roles being generated</param>
    /// <returns>Variant bots</returns>
    public static List<GeneratedBot> CreateVariants(IEnumerable<BotType> botTypes)
    {
        return botTypes
            .Where(_variantDifficulties.ContainsKey)
            .SelectMany(type => _variantDifficulties[type].Select(difficulty => new GeneratedBot(type, difficulty)))
            .ToList();
    }

    /// <summary>
    ///     Get the key a dumped bot is collected under, its role or its role and difficulty when that difficulty is a variant
    /// </summary>
    /// <param name="type">Role of the dumped bot</param>
    /// <param name="difficulty">Difficulty of the dumped bot</param>
    /// <returns>Collection key</returns>
    public static string Key(BotType type, string? difficulty)
    {
        return difficulty is not null
            && _variantDifficulties.TryGetValue(type, out var difficulties)
            && difficulties.Contains(difficulty, StringComparer.OrdinalIgnoreCase)
            ? $"{type}/{difficulty.ToLowerInvariant()}"
            : type.ToString();
    }

    /// <summary>
    ///     Write a learned variant into its role's template
    /// </summary>
    /// <param name="baseBot">Role the variant belongs to</param>
    /// <param name="variant">Learned variant</param>
    public static void MergeInto(GeneratedBot baseBot, GeneratedBot variant)
    {
        baseBot.Data.DifficultyVariants ??= [];
        baseBot.Data.DifficultyVariants[variant.VariantDifficulty!] = new BotTypeVariant
        {
            BotAppearance = variant.Data.BotAppearance,
            BotChances = variant.Data.BotChances,
            FirstNames = variant.Data.FirstNames,
            BotGeneration = variant.Data.BotGeneration,
            BotInventory = variant.Data.BotInventory,
        };
    }
}
