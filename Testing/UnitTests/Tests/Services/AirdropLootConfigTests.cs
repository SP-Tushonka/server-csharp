using NUnit.Framework;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace UnitTests.Tests.Services;

[TestFixture]
public class AirdropLootConfigTests
{
    // TarCoins are an account balance, a crate that may hold money must not hand them out as items
    [Test]
    public void EveryAirdropTypeWithMoney_BlacklistsTarcoin()
    {
        var moneyTypes = DI.GetInstance()
            .GetService<AirdropConfig>()
            .Loot.Where(loot => loot.Value.ItemTypeWhitelist.Contains(BaseClasses.MONEY))
            .ToList();

        Assert.That(moneyTypes, Is.Not.Empty);
        foreach (var (type, loot) in moneyTypes)
        {
            Assert.That(loot.ItemBlacklist, Does.Contain(ItemTpl.MONEY_TARCOIN), type);
        }
    }
}
