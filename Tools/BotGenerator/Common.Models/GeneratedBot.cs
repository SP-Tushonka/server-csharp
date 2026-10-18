using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using Tables = SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace Common.Models
{
    public class GeneratedBot
    {
        // Insertion order decides the key order in the written file
        public static readonly EquipmentSlots[] SlotOrder =
        [
            EquipmentSlots.Headwear,
            EquipmentSlots.Earpiece,
            EquipmentSlots.FaceCover,
            EquipmentSlots.ArmorVest,
            EquipmentSlots.Eyewear,
            EquipmentSlots.ArmBand,
            EquipmentSlots.TacticalVest,
            EquipmentSlots.Backpack,
            EquipmentSlots.FirstPrimaryWeapon,
            EquipmentSlots.SecondPrimaryWeapon,
            EquipmentSlots.Holster,
            EquipmentSlots.Scabbard,
            EquipmentSlots.Pockets,
            EquipmentSlots.SecuredContainer,
        ];

        public GeneratedBot(BotType role, string? variantDifficulty = null)
        {
            Role = role;
            VariantDifficulty = variantDifficulty;
            Data = new Tables.BotType
            {
                BotAppearance = new Appearance
                {
                    Body = new Dictionary<MongoId, double>(),
                    Feet = new Dictionary<MongoId, double>(),
                    Hands = new Dictionary<MongoId, double>(),
                    Head = new Dictionary<MongoId, double>(),
                    Voice = new Dictionary<MongoId, double>(),
                },
                BotExperience = new Experience
                {
                    Level = new MinMax<int>(0, 1),
                    Reward = new Dictionary<string, MinMax<int>>(),
                    StandingForKill = new Dictionary<string, double>(),
                    AggressorBonus = new Dictionary<string, double>(),
                },
                BotHealth = new BotTypeHealth
                {
                    Hydration = new MinMax<double>(100, 100),
                    Energy = new MinMax<double>(100, 100),
                    Temperature = new MinMax<double>(36, 40),
                    BodyParts = BodyParts,
                },
                BotSkills = new BotDbSkills { Common = new Dictionary<string, MinMax<double>>() },
                BotInventory = new BotTypeInventory
                {
                    Equipment = new Dictionary<EquipmentSlots, Dictionary<MongoId, double>>(),
                    Ammo = new Dictionary<string, Dictionary<MongoId, double>>(),
                    Mods = new Dictionary<MongoId, Dictionary<string, HashSet<MongoId>>>(),
                    Items = new ItemPools
                    {
                        TacticalVest = new Dictionary<MongoId, double>(),
                        Pockets = new Dictionary<MongoId, double>(),
                        Backpack = new Dictionary<MongoId, double>(),
                        SecuredContainer = new Dictionary<MongoId, double>(),
                        SpecialLoot = new Dictionary<MongoId, double>(),
                    },
                },
                FirstNames = new List<string>(),
                LastNames = LastNames,
                BotDifficulty = new Dictionary<string, DifficultyCategories>(),
                BotChances = new Chances { EquipmentChances = new Dictionary<string, double>() },
                BotGeneration = new Generation { Items = new GenerationWeightingItems() },
            };

            foreach (var slot in SlotOrder)
            {
                Data.BotInventory.Equipment[slot] = new Dictionary<MongoId, double>();
                Data.BotChances.EquipmentChances[slot.ToString()] = 0;
            }
        }

        public BotType Role { get; }

        // Difficulty this bot holds the loadout of, written into the role's difficultyVariants instead of a file of its own
        public string? VariantDifficulty { get; }

        public string Key
        {
            get { return VariantDifficulty is null ? Role.ToString() : $"{Role}/{VariantDifficulty}"; }
        }

        public int BotCount { get; set; }

        // Bots with a primary weapon, and those of them that also carry a pistol
        public int PrimaryWeaponCount { get; set; }
        public int HolsterWithPrimaryCount { get; set; }

        // Bots with an empty equipment slot, keyed by slot then by the items worn in the slots rolled before it
        public Dictionary<string, Dictionary<string, int>> EmptySlotsAfter { get; } = [];
        public Tables.BotType Data { get; }

        // The server record exposes these as IEnumerable, so the generator appends through these shared lists
        public List<string> LastNames { get; } = new();
        public List<BodyPart> BodyParts { get; } = new();

        // Every distinct build seen, keyed by item template then by the build's structure
        public Dictionary<MongoId, Dictionary<string, ItemBuildSample>> WeaponBuildSamples { get; } = [];
        public Dictionary<MongoId, Dictionary<string, ItemBuildSample>> EquipmentBuildSamples { get; } = [];

        // Live bots per item count, keyed by loot category then by count
        public Dictionary<string, Dictionary<int, int>> GenerationCounts { get; } = [];

        // Rigs worn by bots that also wear body armor
        public Dictionary<MongoId, double> RigsWornWithArmor { get; } = [];

        // Live items per container, keyed by loot category or weapon slot then by container
        public Dictionary<string, Dictionary<string, int>> PlacementCounts { get; } = [];
    }

    public class ItemBuildSample
    {
        public int Count { get; set; }

        // Items of the first bot seen with this build, root item first
        public List<Item> Items { get; init; } = [];
    }
}
