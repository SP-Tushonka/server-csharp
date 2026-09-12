using NUnit.Framework;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace UnitTests.Tests.Generators;

[TestFixture]
public class LocationLootGeneratorTests
{
    // Shoreline's quest hard drive has one template id and thirteen captured positions
    private const string HardDrive = "item_barter_electr_hdd_NosQuests_quest";

    private LocationLootGenerator _generator = default!;
    private LocationTable _locations = default!;
    private ICloner _cloner = default!;

    [SetUp]
    public void Setup()
    {
        _generator = DI.GetInstance().GetService<LocationLootGenerator>();
        _locations = DI.GetInstance().GetService<LocationTable>();
        _cloner = DI.GetInstance().GetService<ICloner>();
    }

    [Test]
    public void GenerateDynamicLoot_ForcedQuestItem_OnePerRaidAtVaryingPositions()
    {
        var shoreline = _locations.GetLocation("shoreline")!;
        var positions = new HashSet<string>();

        for (var raid = 0; raid < 40; raid++)
        {
            var loot = _generator.GenerateDynamicLoot(
                _cloner.Clone(shoreline.LooseLoot!.Value)!,
                _cloner.Clone(shoreline.StaticAmmo)!,
                "shoreline"
            );
            var drives = loot.Where(point => point.Id!.StartsWith(HardDrive)).ToList();
            Assert.That(drives, Has.Count.EqualTo(1), "one hard drive per raid");
            var position = drives[0].Position!.Value;
            positions.Add($"{position.X},{position.Y},{position.Z}");
        }

        Assert.That(positions.Count, Is.GreaterThan(1), "the hard drive should move between raids");
    }
}
