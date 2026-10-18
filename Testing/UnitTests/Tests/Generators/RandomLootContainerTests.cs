using NUnit.Framework;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace UnitTests.Tests.Generators;

[TestFixture]
public class RandomLootContainerTests
{
    private static readonly MongoId AkmnCrate = new("6a15aa0d816d808b87018fba");
    private static readonly MongoId ScourgePackage = new("6ac4eccad8aa0d51df019974");
    private static readonly MongoId ScourgeSuitcase = new("6ac4e0e576842fabb501d5d0");
    private static readonly MongoId Precursor = new("6ac4d528d8aa0d51df01995c");
    private static readonly MongoId Akmn = new("5a0ec13bfcdbcb00165aa685");

    private LootGenerator _generator = default!;
    private ItemHelper _itemHelper = default!;
    private InventoryConfig _config = default!;

    [SetUp]
    public void Setup()
    {
        _generator = DI.GetInstance().GetService<LootGenerator>();
        _itemHelper = DI.GetInstance().GetService<ItemHelper>();
        _config = DI.GetInstance().GetService<InventoryConfig>();
    }

    [Test]
    public void GetRandomLootContainerLoot_WoodenGearCrate_GivesTheFullKit()
    {
        var rewards = _generator.GetRandomLootContainerLoot(_config.RandomLootContainers[AkmnCrate]);

        Assert.That(rewards, Has.Count.EqualTo(33));
        Assert.That(rewards.Single(reward => reward[0].Template == Akmn), Has.Count.GreaterThan(1), "weapon comes assembled");
        var ammoBoxes = rewards.Where(reward => _itemHelper.IsOfBaseclass(reward[0].Template, BaseClasses.AMMO_BOX)).ToList();
        Assert.That(ammoBoxes, Has.Count.EqualTo(7));
        Assert.That(ammoBoxes.All(box => box.Count > 1), Is.True, "ammo boxes hold cartridges");
        Assert.That(rewards.Count(reward => _itemHelper.IsOfBaseclass(reward[0].Template, BaseClasses.VEST)), Is.EqualTo(2));
    }

    [Test]
    public void GetRandomLootContainerLoot_ScourgeContainers_AlwaysHoldThePrecursor()
    {
        var package = _generator.GetRandomLootContainerLoot(_config.RandomLootContainers[ScourgePackage]);
        var suitcase = _generator.GetRandomLootContainerLoot(_config.RandomLootContainers[ScourgeSuitcase]);

        Assert.That(package, Has.Count.EqualTo(9));
        Assert.That(package.Count(reward => reward[0].Template == Precursor), Is.EqualTo(1));
        Assert.That(suitcase.Count(reward => reward[0].Template == Precursor), Is.EqualTo(1));
        Assert.That(suitcase.Single(reward => reward[0].Template == Money.EUROS)[0].Upd!.StackObjectsCount, Is.InRange(1791, 3981));
    }
}
