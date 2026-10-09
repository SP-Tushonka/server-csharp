using NUnit.Framework;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using Item = SPTarkov.Server.Core.Models.Eft.Common.Tables.Item;

namespace UnitTests.Tests.Generators;

[TestFixture]
public class BotWeaponGeneratorTests
{
    private BotWeaponGenerator _botWeaponGenerator;
    private InventoryHelper _inventoryHelper;
    private SaveServer _saveServer;
    private BotTable _botTable;

    [OneTimeSetUp]
    public void Initialize()
    {
        _botWeaponGenerator = DI.GetInstance().GetService<BotWeaponGenerator>();
        _inventoryHelper = DI.GetInstance().GetService<InventoryHelper>();
        _saveServer = DI.GetInstance().GetService<SaveServer>();
        _botTable = DI.GetInstance().GetService<BotTable>();
    }

    [Test]
    public void GenerateWeaponByTpl_generate_m4_pmc()
    {
        var usecTemplate = _botTable.Types["pmcusec"];
        var botTemplateInventory = usecTemplate.BotInventory;

        // Create profile stub to allow `GenerateWeaponByTpl` to work
        var sessionId = new MongoId();
        _saveServer.CreateProfile(new Info() { ProfileId = sessionId });

        var weaponTpl = ItemTpl.ASSAULTRIFLE_COLT_M4A1_556X45_ASSAULT_RIFLE;
        const string slotName = "FirstPrimaryWeapon";
        var weaponModChances = usecTemplate.BotChances.WeaponModsChances;
        foreach (var (key, _) in weaponModChances)
        {
            // Set all mods to 100%
            weaponModChances[key] = 100d;
        }

        var weaponParentId = new MongoId();
        var botGen = new BotGenerationDetails
        {
            Role = "pmcUSEC",
            RoleLowercase = "pmcusec",
            BotLevel = 69,
            IsPmc = true,
        };

        for (var i = 0; i < 100; i++)
        {
            var result = _botWeaponGenerator.GenerateWeaponByTpl(
                sessionId,
                weaponTpl,
                slotName,
                botTemplateInventory,
                weaponParentId,
                weaponModChances,
                botGen
            );

            var itemSize = _inventoryHelper.GetItemSize(weaponTpl, result.Weapon[0].Id, result.Weapon);

            Assert.AreEqual(weaponTpl, result.WeaponTemplate.Id);

            // Ensure it's bigger than just weapon lower
            Assert.AreNotEqual(2, itemSize.Item1);
            Assert.AreNotEqual(1, itemSize.Item2);
        }
    }

    [Test]
    public void GenerateWeaponByTpl_uses_recorded_build()
    {
        var template = _botTable.Types["blackdivision"];
        var weaponTpl = ItemTpl.ASSAULTRIFLE_COLT_M4A1_556X45_ASSAULT_RIFLE;
        var builds = template.BotInventory.WeaponBuilds[weaponTpl].Select(build => Signature(build.Items[0], build.Items)).ToHashSet();

        var sessionId = new MongoId();
        _saveServer.CreateProfile(new Info() { ProfileId = sessionId });
        var botGen = new BotGenerationDetails
        {
            Role = "blackDivision",
            RoleLowercase = "blackdivision",
            BotLevel = 60,
        };

        for (var i = 0; i < 100; i++)
        {
            var result = _botWeaponGenerator.GenerateWeaponByTpl(
                sessionId,
                weaponTpl,
                "FirstPrimaryWeapon",
                template.BotInventory,
                new MongoId(),
                template.BotChances.WeaponModsChances,
                botGen
            );

            Assert.IsTrue(builds.Contains(Signature(result.Weapon[0], result.Weapon)));
        }
    }

    private static string Signature(Item item, List<Item> items)
    {
        var mods = items
            .Where(child => child.ParentId == item.Id.ToString() && child.SlotId != "cartridges" && !child.SlotId.StartsWith("patron_in_weapon"))
            .OrderBy(child => child.SlotId, StringComparer.Ordinal)
            .Select(child => $"{child.SlotId}={Signature(child, items)}");

        return $"{item.Template}({string.Join(",", mods)})";
    }
}
