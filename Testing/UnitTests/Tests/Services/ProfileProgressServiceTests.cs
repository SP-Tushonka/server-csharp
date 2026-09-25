using NUnit.Framework;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Services.InRaid;

namespace UnitTests.Tests.Services;

[TestFixture]
public class ProfileProgressServiceTests
{
    private const string Terminal = "Terminal";
    private static readonly MongoId Makarov = new("5448bd6b4bdc2dfc2f8b4569");
    private static readonly MongoId PierDoorKey = new("6866adbe09b973bf45094339");
    private static readonly MongoId KermanKeycard = new("67c033fd0610e91bea056998");
    private static readonly MongoId ChestRig = new("5648a69d4bdc2ded0b8b457b");
    private static readonly MongoId Pockets = new("557ffd194bdc2d28148b457f");
    private static readonly MongoId Equipment = new("55d7217a4bdc2d86028b456d");

    private ProfileProgressService _service = default!;

    [SetUp]
    public void Setup()
    {
        _service = DI.GetInstance().GetService<ProfileProgressService>();
    }

    [Test]
    public void RemoveListedItems_Terminal_DropsTheKeycardAndKeepsTheGear()
    {
        var profile = Profile();
        var items = profile.CharacterData!.PmcData!.Inventory!.Items!;

        var changed = _service.RemoveListedItems(profile, Terminal);

        Assert.That(changed, Is.True);
        Assert.That(items.Select(item => item.Template), Is.EquivalentTo(new[] { Equipment, Makarov, ChestRig, Pockets, PierDoorKey }));
    }

    [Test]
    public void RemoveListedItems_OtherMap_LeavesTheProfileAlone()
    {
        var profile = Profile();

        var changed = _service.RemoveListedItems(profile, "bigmap");

        Assert.That(changed, Is.False);
        Assert.That(profile.CharacterData!.PmcData!.Inventory!.Items!.Count, Is.EqualTo(6));
    }

    [Test]
    public void GetOptions_Terminal_SavesNoItems()
    {
        Assert.That(_service.GetOptions(Terminal)?.SaveItems, Is.False);
    }

    private static SptProfile Profile()
    {
        var equipmentId = new MongoId();
        var pocketsId = new MongoId();
        var items = new List<Item>
        {
            new() { Id = equipmentId, Template = Equipment },
            new()
            {
                Id = new MongoId(),
                Template = Makarov,
                ParentId = equipmentId,
                SlotId = "FirstPrimaryWeapon",
            },
            new()
            {
                Id = new MongoId(),
                Template = ChestRig,
                ParentId = equipmentId,
                SlotId = "TacticalVest",
            },
            new()
            {
                Id = pocketsId,
                Template = Pockets,
                ParentId = equipmentId,
                SlotId = "Pockets",
            },
            new()
            {
                Id = new MongoId(),
                Template = PierDoorKey,
                ParentId = pocketsId,
                SlotId = "pocket1",
            },
            new()
            {
                Id = new MongoId(),
                Template = KermanKeycard,
                ParentId = pocketsId,
                SlotId = "pocket2",
            },
        };

        return new SptProfile
        {
            CharacterData = new Characters
            {
                PmcData = new PmcData
                {
                    Inventory = new BotBaseInventory { Items = items, Equipment = equipmentId },
                },
            },
        };
    }
}
