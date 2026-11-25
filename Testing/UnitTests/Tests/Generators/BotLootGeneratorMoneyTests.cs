using NUnit.Framework;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace UnitTests.Tests.Generators;

[TestFixture]
public class BotLootGeneratorMoneyTests
{
    private BotLootGenerator _botLootGenerator = default!;
    private ItemHelper _itemHelper = default!;

    [OneTimeSetUp]
    public void Initialize()
    {
        _botLootGenerator = DI.GetInstance().GetService<BotLootGenerator>();
        _itemHelper = DI.GetInstance().GetService<ItemHelper>();
    }

    [TestCase("569668774bdc2da2298b4568")]
    [TestCase("5696686a4bdc2da3298b456a")]
    public void RandomiseMoneyStackSize_BossWedge_GetsBossSizedForeignCurrencyStacks(string currencyTpl)
    {
        var tpl = new MongoId(currencyTpl);
        var template = _itemHelper.GetItem(tpl).Value;

        for (var i = 0; i < 200; i++)
        {
            var money = new Item { Id = new MongoId(), Template = tpl };

            _botLootGenerator.RandomiseMoneyStackSize("bosswedge", template!, money);

            Assert.That(money.Upd!.StackObjectsCount, Is.InRange(2500, 5000));
        }
    }

    [Test]
    public void RandomiseMoneyStackSize_UnlistedRole_FallsBackToDefaultStacks()
    {
        var template = _itemHelper.GetItem(Money.EUROS).Value;

        for (var i = 0; i < 200; i++)
        {
            var money = new Item { Id = new MongoId(), Template = Money.EUROS };

            _botLootGenerator.RandomiseMoneyStackSize("followerwedgelab", template!, money);

            Assert.That(money.Upd!.StackObjectsCount, Is.InRange(50, 250));
        }
    }
}
