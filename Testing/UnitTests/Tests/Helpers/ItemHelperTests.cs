using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace UnitTests.Tests.Helpers;

[TestFixture]
public class ItemHelperTests
{
    private static readonly MongoId Makarov = new("5448bd6b4bdc2dfc2f8b4569");

    private ItemHelper _itemHelper = default!;
    private TemplateTable _templateTable = default!;
    private MongoId _itemWithoutHandbookEntry;

    [SetUp]
    public void Setup()
    {
        _itemHelper = DI.GetInstance().GetService<ItemHelper>();
        _templateTable = DI.GetInstance().GetService<TemplateTable>();

        // Picked from the data rather than hardcoded, a client release can give any one item a
        // handbook entry and that would silently void the premise of the test below
        var listed = _templateTable.Handbook.Items.Select(item => item.Id).ToHashSet();
        _itemWithoutHandbookEntry = _templateTable
            .Items.First(entry => entry.Value.Type == "Item" && !listed.Contains(entry.Key))
            .Key;
    }

    [TearDown]
    public void TearDown()
    {
        _templateTable.Prices.Remove(_itemWithoutHandbookEntry);
    }

    [Test]
    public void IsValidItem_HandbookItem_IsValid()
    {
        Assert.That(_itemHelper.IsValidItem(Makarov), Is.True);
    }

    [Test]
    public void IsValidItem_FleaPriceWithoutHandbookEntry_IsInvalid()
    {
        _templateTable.Prices[_itemWithoutHandbookEntry] = 10000;

        Assert.That(_itemHelper.IsValidItem(_itemWithoutHandbookEntry), Is.False);
    }

    [Test]
    public void Prices_EveryPricedItem_IsListedInTheHandbook()
    {
        var listed = _templateTable.Handbook.Items.Select(item => item.Id).ToHashSet();

        Assert.That(_templateTable.Prices.Keys.Where(tpl => !listed.Contains(tpl)), Is.Empty);
    }
}
