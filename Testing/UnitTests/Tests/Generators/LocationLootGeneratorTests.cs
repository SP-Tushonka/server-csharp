using NUnit.Framework;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Server;
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
            var position = drives[0].Position;
            positions.Add($"{position.X},{position.Y},{position.Z}");
        }

        Assert.That(positions.Count, Is.GreaterThan(1), "the hard drive should move between raids");
    }

    [Test]
    public void GenerateDynamicLoot_ScourgeEventActive_SpawnsOneSuitcaseOnLabs()
    {
        DI.GetInstance().GetService<SeasonalEventService>().ForceSeasonalEvent(SeasonalEventType.Scourge);
        var labs = _locations.GetLocation("laboratory")!;
        var positions = new HashSet<string>();

        for (var raid = 0; raid < 20; raid++)
        {
            var loot = _generator.GenerateDynamicLoot(_cloner.Clone(labs.LooseLoot!.Value)!, _cloner.Clone(labs.StaticAmmo)!, "laboratory");
            var suitcases = loot.Where(point => point.Id!.StartsWith("item_container_event_scourge_suitcase")).ToList();
            Assert.That(suitcases, Has.Count.EqualTo(1));
            positions.Add($"{suitcases[0].Position.X},{suitcases[0].Position.Z}");
        }

        Assert.That(positions, Has.Count.EqualTo(2), "the suitcase alternates between its two spots");
    }

    [Test]
    public void Statics_EveryRandomisedContainer_HasAGroup()
    {
        var missing = new List<string>();
        foreach (
            var locationId in new[]
            {
                "bigmap",
                "factory4_day",
                "factory4_night",
                "interchange",
                "laboratory",
                "labyrinth",
                "lighthouse",
                "rezervbase",
                "sandbox",
                "sandbox_high",
                "shoreline",
                "tarkovstreets",
                "woods",
                "factory_event",
                "laboratory_event",
            }
        )
        {
            var location = _locations.GetLocation(locationId)!;
            var groups = location.Statics!.Containers!;
            missing.AddRange(
                location
                    .StaticContainers!.Value!.StaticContainers!.Where(container =>
                        container.Probability < 1 && !container.Template!.IsAlwaysSpawn
                    )
                    .Where(container => !groups.ContainsKey(container.Template!.Id!))
                    .Select(container => $"{locationId}/{container.Template!.Id}")
            );
        }

        Assert.That(missing, Is.Empty);
    }

    [TestCase("factory_event", "factory4_day", 1.0)]
    [TestCase("laboratory_event", "laboratory", 0.05)]
    public void LooseLoot_EventMaps_HaveTheirOwnPositions(string eventMap, string baseMap, double minimumOwnShare)
    {
        // Both event maps shipped as a copy of the base map's loot, which put every factory_event
        // item at a position that map does not have
        var eventPositions = LoosePositions(eventMap);
        var basePositions = LoosePositions(baseMap);
        var ownShare = (double)eventPositions.Except(basePositions).Count() / eventPositions.Count;

        Assert.That(ownShare, Is.GreaterThanOrEqualTo(minimumOwnShare), $"{eventMap} positions not found on {baseMap}");
    }

    [TestCase("factory_event")]
    [TestCase("laboratory_event")]
    public void GenerateStaticContainers_EventMaps_ProduceLoot(string locationId)
    {
        var containers = _generator.GenerateStaticContainers(locationId, _cloner.Clone(_locations.GetLocation(locationId)!.StaticAmmo)!);

        Assert.That(containers, Is.Not.Empty);
        Assert.That(containers.Sum(container => container.Items!.Count()), Is.GreaterThan(0));
    }

    private HashSet<(double, double, double)> LoosePositions(string locationId)
    {
        var looseLoot = _locations.GetLocation(locationId)!.LooseLoot!.Value!;

        return looseLoot
            .Spawnpoints!.Concat(looseLoot.SpawnpointsForced!)
            .Select(point => (
                Math.Round(point.Template!.Position!.X, 2),
                Math.Round(point.Template.Position.Y, 2),
                Math.Round(point.Template.Position.Z, 2)
            ))
            .ToHashSet();
    }
}
