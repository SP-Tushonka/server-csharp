using System.Text.Json;
using NUnit.Framework;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace UnitTests.Tests.Helpers;

[TestFixture]
public sealed class AchievementCompatibilityHelperTests
{
    private static QuestCondition Condition(string type) => new()
    {
        Id = new MongoId("000000000000000000000001"),
        DynamicLocale = false,
        ConditionType = type,
    };

    private static Achievement Achievement(int index, params QuestCondition[] conditions) => new()
    {
        Index = index,
        Id = new MongoId(index.ToString("x24")),
        ImageUrl = "fixture.png",
        AssetPath = "",
        Rewards = [],
        Conditions = new AchievementQuestConditionTypes { AvailableForFinish = [.. conditions], Fail = [] },
        InstantComplete = false,
        ShowNotificationsInGame = false,
        ShowProgress = false,
        prefab = "",
        Rarity = "Common",
        Hidden = false,
        ShowConditions = true,
        ProgressBarEnabled = false,
        Side = "Pmc",
    };

    [Test]
    public void EmptyPolicyPreservesAllEntriesAndReturnsAnIndependentList()
    {
        var input = new List<Achievement> { Achievement(1, Condition("LeagueRank")), Achievement(2) };
        var result = AchievementCompatibilityHelper.FilterUnsupported(input, []);
        Assert.That(result, Is.EqualTo(input));
        Assert.That(result, Is.Not.SameAs(input));
        Assert.That(AchievementCompatibilityHelper.FilterUnsupported(input, null), Is.EqualTo(input));
    }

    [Test]
    public void FeaturePolicyDefaultsToEmpty()
    {
        var features = new ServerFeatures
        {
            ChatbotFeatures = null!,
            CreateNewProfileTypesBlacklist = [],
            AchievementProfileIdBlacklist = [],
        };
        Assert.That(features.UnsupportedAchievementConditionTypes, Is.Empty);
    }

    [Test]
    public void ExplicitCompatibilityPolicyDeserializesWithoutChangingDefaultCasing()
    {
        var features = JsonSerializer.Deserialize<ServerFeatures>(
            """{"chatbotFeatures":null,"createNewProfileTypesBlacklist":[],"achievementProfileIdBlacklist":[],"unsupportedAchievementConditionTypes":["LeagueRank"]}"""
        )!;
        Assert.That(features.UnsupportedAchievementConditionTypes, Does.Contain("LeagueRank"));
        Assert.That(features.UnsupportedAchievementConditionTypes.Contains("leagueRank"), Is.False);
    }

    [TestCase("Started")]
    [TestCase("AvailableForFinish")]
    [TestCase("AvailableForStart")]
    [TestCase("Success")]
    [TestCase("Fail")]
    public void EachConditionGroupCanExcludeTheWholeAchievement(string group)
    {
        var item = Achievement(1);
        var conditions = new List<QuestCondition> { Condition("LeagueRank"), Condition("Level") };
        switch (group)
        {
            case "Started": item.Conditions.Started = conditions; break;
            case "AvailableForFinish": item.Conditions.AvailableForFinish = conditions; break;
            case "AvailableForStart": item.Conditions.AvailableForStart = conditions; break;
            case "Success": item.Conditions.Success = conditions; break;
            case "Fail": item.Conditions.Fail = conditions; break;
        }
        Assert.That(AchievementCompatibilityHelper.FilterUnsupported([item], ["LeagueRank"]), Is.Empty);
        Assert.That(conditions, Has.Count.EqualTo(2));
    }

    [Test]
    public void NestedCounterConditionExcludesWholeAchievement()
    {
        var root = Condition("CounterCreator");
        root.Counter = new QuestConditionCounter
        {
            Id = "counter",
            Conditions = [new QuestConditionCounterCondition { ConditionType = "LeagueRank" }],
        };
        Assert.That(AchievementCompatibilityHelper.FilterUnsupported([Achievement(1, root)], ["LeagueRank"]), Is.Empty);
        Assert.That(root.Counter.Conditions, Has.Count.EqualTo(1));
    }

    [Test]
    public void TypedVisibilityConditionIsInspected()
    {
        var root = Condition("Level");
        root.VisibilityConditions = [new VisibilityCondition { Id = "visibility", Target = "fixture", ConditionType = "LeagueRank" }];
        Assert.That(AchievementCompatibilityHelper.FilterUnsupported([Achievement(1, root)], ["LeagueRank"]), Is.Empty);
    }

    [Test]
    public void RetainedObjectsOrderIndicesAndCatalogRemainUnchanged()
    {
        var first = Achievement(10, Condition("Level"));
        first.AssetPath = "LeagueRank";
        var omitted = Achievement(20, Condition("LeagueRank"));
        var last = Achievement(30, Condition("OtherFutureCondition"));
        var input = new List<Achievement> { first, omitted, last };
        var result = AchievementCompatibilityHelper.FilterUnsupported(input, ["LeagueRank"]);
        Assert.That(result, Is.EqualTo(new[] { first, last }));
        Assert.That(result[0], Is.SameAs(first));
        Assert.That(result[1], Is.SameAs(last));
        Assert.That(result.Select(item => item.Index), Is.EqualTo(new[] { 10, 30 }));
        Assert.That(input, Has.Count.EqualTo(3));
        Assert.That(omitted.Conditions.AvailableForFinish[0].ConditionType, Is.EqualTo("LeagueRank"));
        Assert.That(AchievementCompatibilityHelper.FilterUnsupported(input, ["LeagueRank"]), Is.EqualTo(result));
    }

    [Test]
    public void MatchingUsesOrdinalCaseSensitiveDiscriminators()
    {
        var item = Achievement(1, Condition("leagueRank"));
        Assert.That(AchievementCompatibilityHelper.FilterUnsupported([item], ["LeagueRank"]), Has.Count.EqualTo(1));
    }

    [Test]
    public void OptionalNullAndEmptyCollectionsRemainSupported()
    {
        var item = Achievement(1);
        Assert.That(AchievementCompatibilityHelper.FilterUnsupported([item], ["LeagueRank"]), Has.Count.EqualTo(1));
        Assert.That(AchievementCompatibilityHelper.FilterUnsupported([], ["LeagueRank"]), Is.Empty);
    }
}
