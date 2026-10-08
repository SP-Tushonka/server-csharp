using NUnit.Framework;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Items;

namespace UnitTests.Tests.Services;

[TestFixture]
public class StoryItemAvailabilityTests
{
    private static readonly MongoId[] StoryItems =
    [
        new("6866adbe09b973bf45094339"), // Pier door key
        new("6866ad3853330f9b83064cf9"), // Black Division keycard
        new("68d152b9915f131d4e05ee6c"), // Voevoda's audio recorder
        new("68e6394e658d876c930977b1"), // Unknown device fragment
    ];

    [Test]
    public void RewardBlacklist_StoryItemsTradersRefuse_AreBlacklisted()
    {
        var itemFilterService = DI.GetInstance().GetService<ItemFilterService>();

        Assert.That(StoryItems.Where(tpl => !itemFilterService.IsItemRewardBlacklisted(tpl)), Is.Empty);
    }

    [Test]
    public void FenceBaseAssort_StoryItemsTradersRefuse_AreNotSold()
    {
        var fence = DI.GetInstance().GetService<TradersTable>().GetTrader(Traders.FENCE)!;
        var sold = fence.Assort!.Items.Select(item => item.Template).ToHashSet();

        Assert.That(StoryItems.Where(sold.Contains), Is.Empty);
    }

    [Test]
    public void FenceBaseAssort_DogtagsAndFlaggedStoryItems_AreNotSold()
    {
        var fence = DI.GetInstance().GetService<TradersTable>().GetTrader(Traders.FENCE)!;
        var sold = fence.Assort!.Items.Select(item => item.Template).Distinct();

        Assert.That(sold.Where(IsDogtagOrStoryItem), Is.Empty);
    }

    [TestCase("pmcbear")]
    [TestCase("pmcusec")]
    public void PmcLootPools_DogtagsAndFlaggedStoryItems_AreExcluded(string role)
    {
        var generator = DI.GetInstance().GetService<PMCLootGenerator>();
        var pooled = generator
            .GeneratePMCBackpackLootPool(role)
            .Keys.Concat(generator.GeneratePMCVestLootPool(role).Keys)
            .Concat(generator.GeneratePMCPocketLootPool(role).Keys);

        Assert.That(pooled.Where(IsDogtagOrStoryItem), Is.Empty);
    }

    private static bool IsDogtagOrStoryItem(MongoId tpl)
    {
        var item = DI.GetInstance().GetService<ItemHelper>().GetItem(tpl).Value;

        return item is not null && (item.IsDogtag() || item.IsStoryItem());
    }
}
