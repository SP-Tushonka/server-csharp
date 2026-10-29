using System.Text.Json.Nodes;
using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Migration;
using SPTarkov.Server.Core.Migration.Migrations._5._0;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace UnitTests.Tests.Migration;

[TestFixture]
public class ProfileRepairMigrationTests
{
    private const string BlueFireKeep = "67b892ba0e9aad30720bfc56";
    private const string BlueFireSell = "67b892b832f50fb7ec084426";
    private const string TheyAreAlreadyHere = "6903d779fdfc4078740a4bd0";
    private const string TheyAreAlreadyHereStartVariable = "690941b7e110c6855b7427ff";

    [Test]
    public void RemoveDuplicateQuests_RepeatedEntry_KeepsTheFirst()
    {
        var migration = new RemoveDuplicateQuests();
        var profile = Profile(Quest(BlueFireKeep, 4), Quest(BlueFireKeep, 2), Quest(BlueFireSell, 5));

        Assert.That(CanMigrate(migration, profile), Is.True);
        migration.Migrate(profile);

        var quests = profile["characters"]!["pmc"]!["Quests"]!.AsArray();
        Assert.That(quests.Select(quest => quest!["qid"]!.GetValue<string>()), Is.EqualTo(new[] { BlueFireKeep, BlueFireSell }));
        Assert.That(quests[0]!["status"]!.GetValue<int>(), Is.EqualTo(4));
        Assert.That(CanMigrate(migration, profile), Is.False);
    }

    [TestCase(2, true)]
    [TestCase(4, true)]
    [TestCase(5, false)]
    [TestCase(9, false)]
    public void AddMissingStartVariables_StartVariableMissing_SetsItOnlyForQuestsThatRan(int status, bool expected)
    {
        var migration = new AddMissingStartVariables(DI.GetInstance().GetService<TemplateTable>());
        var profile = Profile(Quest(TheyAreAlreadyHere, status));

        Assert.That(CanMigrate(migration, profile), Is.EqualTo(expected));
        if (!expected)
        {
            return;
        }

        migration.Migrate(profile);

        Assert.That(profile["characters"]!["pmc"]!["Variables"]![TheyAreAlreadyHereStartVariable]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(CanMigrate(migration, profile), Is.False);
    }

    [TestCase(2, true)]
    [TestCase(5, false)]
    public void RepairStoryChapters_AlternativeLeftRunning_IsPickedUp(int sellStatus, bool expected)
    {
        var migration = new RepairStoryChapters(
            DI.GetInstance().GetService<MainStoryChapterHelper>(),
            DI.GetInstance().GetService<JsonUtil>(),
            DI.GetInstance().GetService<TimeUtil>()
        );

        Assert.That(CanMigrate(migration, Profile(Quest(BlueFireKeep, 4), Quest(BlueFireSell, sellStatus))), Is.EqualTo(expected));
    }

    [TestCase("67499b9b909d2013670a5029", true)]
    [TestCase("5449016a4bdc2d6f028b456f", false)]
    public void MoveQuestItemsToQuestStash_ItemInMainStash_IsPickedUpWhenItBelongsInTheQuestStash(string template, bool expected)
    {
        var migration = DI.GetInstance().GetService<MoveQuestItemsToQuestStash>();
        var profile = Profile();
        profile["characters"]!["pmc"]!["Inventory"] = new JsonObject
        {
            ["stash"] = "5fe4977574f15b4ad31b66b6",
            ["questStashItems"] = "5fe4977574f15b4ad31b66b8",
            ["items"] = new JsonArray(
                new JsonObject { ["_id"] = "5fe4977574f15b4ad31b66b6", ["_tpl"] = "5811ce772459770e9e5f9532" },
                new JsonObject
                {
                    ["_id"] = "676db384777490e23c45b657",
                    ["_tpl"] = template,
                    ["parentId"] = "5fe4977574f15b4ad31b66b6",
                    ["slotId"] = "hideout",
                }
            ),
        };

        Assert.That(CanMigrate(migration, profile), Is.EqualTo(expected));
    }

    private static bool CanMigrate(IProfileMigration migration, JsonObject profile)
    {
        return migration.CanMigrate(profile, new ProfileMigrationContext(), []);
    }

    private static JsonObject Profile(params JsonObject[] quests)
    {
        return new JsonObject { ["characters"] = new JsonObject { ["pmc"] = new JsonObject { ["Quests"] = new JsonArray(quests) } } };
    }

    private static JsonObject Quest(string questId, int status)
    {
        return new JsonObject
        {
            ["qid"] = questId,
            ["status"] = status,
            ["startTime"] = 0,
            ["statusTimers"] = new JsonObject { [status.ToString()] = 0 },
        };
    }
}
