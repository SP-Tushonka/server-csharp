using System.Text.Json.Nodes;
using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Migration.Migrations._5._0;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;

namespace UnitTests.Tests.Helpers;

[TestFixture]
public class PrestigeHelperTests
{
    private const string Daily = "615ffc701c97c768137e719b";
    private const string Weekly = "618035d38012292db3081bf0";

    [Test]
    public void PrestigeLevelAchievements_CoverEveryPrestigeLevel()
    {
        var levels = DI.GetInstance().GetService<TemplateTable>().Prestige.Elements!;

        Assert.That(PrestigeHelper.PrestigeLevelAchievements, Has.Count.EqualTo(levels.Count));
    }

    [Test]
    public void ProcessPendingPrestige_LevelTwo_GrantsItsAchievementAndOnlyItsTarcoins()
    {
        var levels = DI.GetInstance().GetService<TemplateTable>().Prestige.Elements!;
        var sessionId = new MongoId();
        var oldProfile = CreateProfile(sessionId, tarcoins: 0);
        var newProfile = CreateProfile(sessionId, tarcoins: 100);
        DI.GetInstance().GetService<SaveServer>().AddProfile(newProfile);

        DI.GetInstance()
            .GetService<PrestigeHelper>()
            .ProcessPendingPrestige(oldProfile, newProfile, new PendingPrestige { PrestigeLevel = 2, Items = [] });

        var pmc = newProfile.CharacterData!.PmcData!;
        var levelTwoTarcoins = levels[1].Rewards.Where(reward => reward.Type == RewardType.Tarcoin).Sum(reward => (int)reward.Value!);
        Assert.That(pmc.Achievements!.Keys, Does.Contain(PrestigeHelper.PrestigeLevelAchievements[1]));
        Assert.That(pmc.TarCoinBalance, Is.EqualTo(100 + levelTwoTarcoins));
        Assert.That(pmc.Prestige!.Keys, Does.Contain(levels[1].Id));
        Assert.That(pmc.Info!.PrestigeLevels![PrestigeGameModes.Pve], Is.EqualTo(2));
        Assert.That(pmc.Info.PrestigeLevels[PrestigeGameModes.Regular], Is.EqualTo(0));
        Assert.That(pmc.Info.SelectedPrestigeGameMode, Is.EqualTo(PrestigeGameModes.Pve));
        Assert.That(pmc.Info.PrestigeLevel, Is.EqualTo(2));
    }

    [TestCase("""{"PrestigeLevel":3}""", 3)]
    [TestCase("""{"PrestigeLevel":2,"PrestigeLevels":{"regular":2,"pve":2},"SelectedPrestigeGameMode":"regular"}""", 2)]
    public void SplitPrestigeLevelsByMode_SingleOrMirroredLevel_MovesToPve(string info, int expected)
    {
        var profile = JsonNode.Parse("""{"characters":{"pmc":{"Info":""" + info + "}}}")!.AsObject();
        var migration = new SplitPrestigeLevelsByMode();

        Assert.That(migration.CanMigrate(profile, []), Is.True);
        migration.Migrate(profile);

        var migrated = profile["characters"]!["pmc"]!["Info"]!;
        Assert.That(migrated["PrestigeLevels"]!["pve"]!.GetValue<int>(), Is.EqualTo(expected));
        Assert.That(migrated["PrestigeLevels"]!["regular"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(migrated["SelectedPrestigeGameMode"]!.GetValue<string>(), Is.EqualTo("pve"));
        Assert.That(migration.CanMigrate(profile, []), Is.False);
    }

    [Test]
    public void ProcessPendingPrestige_LevelTwo_GrantsItsExtraDailyAndWeekly()
    {
        var sessionId = new MongoId();
        var newProfile = CreateProfile(sessionId, tarcoins: 0);
        DI.GetInstance().GetService<SaveServer>().AddProfile(newProfile);

        DI.GetInstance()
            .GetService<PrestigeHelper>()
            .ProcessPendingPrestige(
                CreateProfile(sessionId, tarcoins: 0),
                newProfile,
                new PendingPrestige { PrestigeLevel = 2, Items = [] }
            );

        Assert.That(newProfile.SptData!.ExtraRepeatableQuests![Daily], Is.EqualTo(1));
        Assert.That(newProfile.SptData.ExtraRepeatableQuests[Weekly], Is.EqualTo(1));
    }

    [Test]
    public void RestorePrestigeExtraRepeatables_ZeroedGrants_AreRebuiltFromClaimedLevels()
    {
        var levels = DI.GetInstance().GetService<TemplateTable>().Prestige.Elements!;
        var profile = new JsonObject
        {
            ["spt"] = new JsonObject
            {
                ["extraRepeatableQuests"] = new JsonObject
                {
                    [Daily] = 0d,
                    [Weekly] = 0d,
                    ["0123456789abcdef01234567"] = 0d,
                },
            },
            ["characters"] = new JsonObject
            {
                ["pmc"] = new JsonObject
                {
                    ["Prestige"] = new JsonObject { [levels[0].Id] = 1, [levels[1].Id] = 2 },
                },
            },
        };
        var migration = new RestorePrestigeExtraRepeatables(DI.GetInstance().GetService<TemplateTable>());

        Assert.That(migration.CanMigrate(profile, []), Is.True);
        migration.Migrate(profile);

        var extras = profile["spt"]!["extraRepeatableQuests"]!.AsObject();
        Assert.That(extras.Select(e => e.Key), Is.EquivalentTo(new[] { Daily, Weekly }));
        Assert.That(extras[Daily]!.GetValue<double>(), Is.EqualTo(1));
        Assert.That(migration.CanMigrate(profile, []), Is.False);
    }

    [Test]
    public void SplitPrestigeLevelsByMode_ProfileWithoutPrestige_IsLeftAlone()
    {
        var profile = JsonNode.Parse("""{"characters":{"pmc":{"Info":{"PrestigeLevels":{"regular":0,"pve":0}}}}}""")!.AsObject();

        Assert.That(new SplitPrestigeLevelsByMode().CanMigrate(profile, []), Is.False);
    }

    [Test]
    public void RekeyPrestigeLevels_ProfileWithOldIds_KeepsClaimTimesUnderNewIds()
    {
        var profile = JsonNode
            .Parse("""{"characters":{"pmc":{"Prestige":{"672df12f97f0469cea52f55e":111,"672df4281ab8d9c8849a0c88":222}}}}""")!
            .AsObject();
        var migration = new RekeyPrestigeLevels();

        Assert.That(migration.CanMigrate(profile, []), Is.True);
        migration.Migrate(profile);

        var prestiges = profile["characters"]!["pmc"]!["Prestige"]!.AsObject();
        Assert.That(prestiges.Select(p => p.Key), Is.EquivalentTo(new[] { "6aad4b9ebe4ab598ffc34517", "6aad4b9ef51244f6fc01b426" }));
        Assert.That(prestiges["6aad4b9ef51244f6fc01b426"]!.GetValue<long>(), Is.EqualTo(222));
        Assert.That(migration.CanMigrate(profile, []), Is.False);
    }

    private static SptProfile CreateProfile(MongoId sessionId, int tarcoins)
    {
        return new SptProfile
        {
            ProfileInfo = new SPTarkov.Server.Core.Models.Eft.Profile.Info { ProfileId = sessionId },
            SptData = new Spt(),
            CustomisationUnlocks = [],
            DialogueRecords = [],
            CharacterData = new Characters
            {
                PmcData = new PmcData
                {
                    Id = sessionId,
                    Info = new SPTarkov.Server.Core.Models.Eft.Common.Tables.Info { Side = "Usec", Level = 1 },
                    Achievements = [],
                    Prestige = [],
                    Skills = new Skills { Common = [], Mastering = [] },
                    Stats = new Stats { Eft = new EftStats() },
                    Inventory = new BotBaseInventory { Items = [] },
                    TarCoinBalance = tarcoins,
                },
            },
        };
    }
}
