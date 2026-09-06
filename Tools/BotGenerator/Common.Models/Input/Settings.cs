using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Common.Models.Converters;

namespace Common.Models.Input
{
    public record Settings
    {
        public string Role { get; set; }
        public string BotDifficulty { get; set; }
        public int Experience { get; set; }

        [JsonConverter(typeof(StringToDoubleConverter))]
        public double StandingForKill { get; set; }

        [JsonConverter(typeof(StringToDoubleConverter))]
        public double AggressorBonus { get; set; }
        public bool UseSimpleAnimator { get; set; }
    }

    public record Info
    {
        public string Nickname { get; set; }
        public string LowerNickname { get; set; }
        public string Side { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int RegistrationDate { get; set; }
        public string GameVersion { get; set; }
        public int AccountType { get; set; }
        public int MemberCategory { get; set; }
        public bool lockedMoveCommands { get; set; }
        public int SavageLockTime { get; set; }
        public int LastTimePlayedAsSavage { get; set; }
        public Settings Settings { get; set; }
        public int NicknameChangeDate { get; set; }
        public List<object> NeedWipeOptions { get; set; }
        public object lastCompletedWipe { get; set; }
        public bool BannedState { get; set; }
        public int BannedUntil { get; set; }
        public bool IsStreamerModeAvailable { get; set; }
    }

    public record Customization
    {
        public string Head { get; set; }
        public string Body { get; set; }
        public string Feet { get; set; }
        public string Hands { get; set; }
        public string Voice { get; set; }
    }

    public record Hydration
    {
        public int Current { get; set; }
        public int Maximum { get; set; }
    }

    public record Energy
    {
        public int Current { get; set; }
        public int Maximum { get; set; }
    }

    public record Temperature
    {
        public double Current { get; set; }
        public int Maximum { get; set; }
    }

    public record Health
    {
        public int Current { get; set; }
        public int Maximum { get; set; }
        public Hydration Hydration { get; set; }
        public Energy Energy { get; set; }
        public Temperature Temperature { get; set; }
        public BodyParts BodyParts { get; set; }
        public int UpdateTime { get; set; }
    }

    public record Head
    {
        public BodyPartHealth Health { get; set; }
    }

    public record Chest
    {
        public BodyPartHealth Health { get; set; }
    }

    public record Stomach
    {
        public BodyPartHealth Health { get; set; }
    }

    public record LeftArm
    {
        public BodyPartHealth Health { get; set; }
    }

    public record RightArm
    {
        public BodyPartHealth Health { get; set; }
    }

    public record LeftLeg
    {
        public BodyPartHealth Health { get; set; }
    }

    public record RightLeg
    {
        public BodyPartHealth Health { get; set; }
    }

    [DataContract(Name = "Health")]
    public record BodyPartHealth
    {
        [DataMember]
        public int Current { get; set; }

        [DataMember]
        public int Maximum { get; set; }
    }

    public record BodyParts
    {
        public Head Head { get; set; }
        public Chest Chest { get; set; }
        public Stomach Stomach { get; set; }
        public LeftArm LeftArm { get; set; }
        public RightArm RightArm { get; set; }
        public LeftLeg LeftLeg { get; set; }
        public RightLeg RightLeg { get; set; }

        public int GetHpMaxTotal()
        {
            return Head.Health.Maximum
                + Chest.Health.Maximum
                + Stomach.Health.Maximum
                + LeftArm.Health.Maximum
                + RightArm.Health.Maximum
                + LeftLeg.Health.Maximum
                + RightLeg.Health.Maximum;
        }
    }

    public record Repairable
    {
        public int Durability { get; set; }
        public int MaxDurability { get; set; }
    }

    public record FoodDrink
    {
        public int HpPercent { get; set; }
    }

    public record FireMode
    {
        [JsonPropertyName("FireMode")]
        public string WeaponFireMode { get; set; }
    }

    public record Foldable
    {
        public bool Folded { get; set; }
    }

    public record MedKit
    {
        public int HpResource { get; set; }
    }

    public record Upd
    {
        public Repairable Repairable { get; set; }
        public int? StackObjectsCount { get; set; }
        public FoodDrink FoodDrink { get; set; }
        public FireMode FireMode { get; set; }
        public Foldable Foldable { get; set; }
        public MedKit MedKit { get; set; }
    }

    public record Location
    {
        public int x { get; set; }
        public int y { get; set; }
        public int r { get; set; }
    }

    public record Item
    {
        public string _id { get; set; }
        public string _tpl { get; set; }
        public string parentId { get; set; }
        public string slotId { get; set; }
        public Upd upd { get; set; }
        public object location { get; set; }
    }

    public record FastPanel { }

    public record Inventory
    {
        public List<Item> items { get; set; }
        public string equipment { get; set; }
        public string stash { get; set; }
        public string sortingTable { get; set; }
        public string questRaidItems { get; set; }
        public string questStashItems { get; set; }
        public FastPanel fastPanel { get; set; }
    }

    public record Skills
    {
        public List<Common> Common { get; set; }
        public List<object> Mastering { get; set; }
        public int Points { get; set; }
    }

    public record Common
    {
        public string Id { get; set; }
        public int Progress { get; set; }
        public int PointsEarnedDuringSession { get; set; }
        public int LastAccess { get; set; }
    }

    public record Stats
    {
        public EftStats Eft { get; set; }
    }

    public record EftStats
    {
        public SessionCounters SessionCounters { get; set; }
        public OverallCounters OverallCounters { get; set; }
    }

    public record SessionCounters
    {
        public List<object> Items { get; set; }
    }

    public record OverallCounters
    {
        public List<object> Items { get; set; }
    }

    public record TaskConditionCounters
    {
        public object Counters { get; set; }
    }

    public record BackendCounters { }

    public record Production { }

    public record Area
    {
        public int type { get; set; }
        public int level { get; set; }
        public bool active { get; set; }
        public bool passiveBonusesEnabled { get; set; }
        public int completeTime { get; set; }
        public bool constructing { get; set; }
        public List<object> slots { get; set; }
        public object lastRecipe { get; set; }
    }

    public record Hideout
    {
        public Production Production { get; set; }
        public List<Area> Areas { get; set; }
    }

    public class Datum
    {
        public string _id { get; set; }
        public int aid { get; set; }
        public object savage { get; set; }
        public Info Info { get; set; }
        public Customization Customization { get; set; }
        public Health Health { get; set; }
        public Inventory Inventory { get; set; }
        public Skills Skills { get; set; }
        public Stats Stats { get; set; }
        public object Encyclopedia { get; set; }
        public TaskConditionCounters TaskConditionCounters { get; set; }
        public List<object> InsuredItems { get; set; }
        public Hideout Hideout { get; set; }
        public IEnumerable<object> Bonuses { get; set; }

        protected bool Equals(Datum other)
        {
            return _id == other._id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((Datum)obj);
        }

        public override int GetHashCode()
        {
            return (_id != null ? _id.GetHashCode() : 0);
        }
    }

    public record Root
    {
        public int err { get; set; }
        public object errmsg { get; set; }
        public IEnumerable<Datum> data { get; set; }
    }
}
