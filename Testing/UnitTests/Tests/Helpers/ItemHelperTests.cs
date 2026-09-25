using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace UnitTests.Tests.Helpers;

[TestFixture]
public class ItemHelperTests
{
    private static readonly MongoId Makarov = new("5448bd6b4bdc2dfc2f8b4569");
    private static readonly MongoId HelmetBuiltInArmor = new("64b11c08506a73f6a10f9364");

    private ItemHelper _itemHelper = default!;
    private TemplateTable _templateTable = default!;

    [SetUp]
    public void Setup()
    {
        _itemHelper = DI.GetInstance().GetService<ItemHelper>();
        _templateTable = DI.GetInstance().GetService<TemplateTable>();
    }

    [TearDown]
    public void TearDown()
    {
        _templateTable.Prices.Remove(HelmetBuiltInArmor);
    }

    [Test]
    public void IsValidItem_HandbookItem_IsValid()
    {
        Assert.That(_itemHelper.IsValidItem(Makarov), Is.True);
    }

    [Test]
    public void IsValidItem_FleaPriceWithoutHandbookEntry_IsInvalid()
    {
        _templateTable.Prices[HelmetBuiltInArmor] = 10000;

        Assert.That(_itemHelper.IsValidItem(HelmetBuiltInArmor), Is.False);
    }

    [Test]
    public void Prices_EveryPricedItem_IsListedInTheHandbook()
    {
        var listed = _templateTable.Handbook.Items.Select(item => item.Id).ToHashSet();

        Assert.That(_templateTable.Prices.Keys.Where(tpl => !listed.Contains(tpl)), Is.Empty);
    }
}
