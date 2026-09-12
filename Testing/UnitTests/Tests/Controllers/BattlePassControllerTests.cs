using NUnit.Framework;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.BattlePass;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Servers;

namespace UnitTests.Tests.Controllers;

[TestFixture]
public class BattlePassControllerTests
{
    private static readonly MongoId Pass = new("6a27da69f3610ccfe5e71ab5");
    private static readonly MongoId FinanceDocument = new("6a31807f17005505b70d5827");
    private static readonly MongoId PersonalScheme = new("6a318b84fab84feb6aa71fe4");
    private static readonly MongoId PersonalDocument = new("6a317b9692cfdcddcb02a58e");
    private static readonly MongoId Container = new("6a4fa628b4831242f306e8cd");
    private static readonly MongoId StandardStash = new("566abbc34bdc2d92178b4576");
    private static readonly MongoId SortingTable = new("602543c13fd1f03fe0c6d15a");

    private BattlePassController _controller = default!;
    private SaveServer _saveServer = default!;
    private MongoId _stack;

    [SetUp]
    public void Setup()
    {
        _controller = DI.GetInstance().GetService<BattlePassController>();
        _saveServer = DI.GetInstance().GetService<SaveServer>();
    }

    [Test]
    public void ExchangeDocuments_FiveForOne_SwapsTheStacks()
    {
        var sessionId = AddProfile(documents: 10);
        var stack = _stack;
        var request = new BattlePassExchangeDocumentsRequest
        {
            BattlePassId = Pass,
            ReceiveDocumentId = PersonalScheme,
            Items = [new BattlePassHandIn { Id = stack, Count = 5 }],
        };

        var output = _controller.ExchangeDocuments(Pmc(sessionId), request, sessionId);

        var items = Pmc(sessionId).Inventory!.Items!;
        Assert.That(output.Warnings, Is.Empty);
        Assert.That(items.First(item => item.Id == stack).Upd!.StackObjectsCount, Is.EqualTo(5));
        Assert.That(items.Single(item => item.Template == PersonalDocument).Upd!.StackObjectsCount, Is.EqualTo(1));
        Assert.That(output.ProfileChanges[sessionId].Items!.NewItems!.Single().Template, Is.EqualTo(PersonalDocument));
    }

    [Test]
    public void ExchangeDocumentsForItem_TenDocuments_BuysTheContainer()
    {
        var sessionId = AddProfile(documents: 10);
        var stack = _stack;
        var request = new BattlePassExchangeDocumentsRequest
        {
            BattlePassId = Pass,
            Items = [new BattlePassHandIn { Id = stack, Count = 10 }],
        };

        var output = _controller.ExchangeDocumentsForItem(Pmc(sessionId), request, sessionId);

        var items = Pmc(sessionId).Inventory!.Items!;
        Assert.That(output.Warnings, Is.Empty);
        Assert.That(items.Any(item => item.Id == stack), Is.False);
        Assert.That(items.Count(item => item.Template == Container), Is.EqualTo(1));
    }

    [Test]
    public void ExchangeDocuments_NotAWholeExchange_IsRefused()
    {
        var sessionId = AddProfile(documents: 10);
        var stack = _stack;
        var request = new BattlePassExchangeDocumentsRequest
        {
            BattlePassId = Pass,
            ReceiveDocumentId = PersonalScheme,
            Items = [new BattlePassHandIn { Id = stack, Count = 7 }],
        };

        var output = _controller.ExchangeDocuments(Pmc(sessionId), request, sessionId);

        Assert.That(output.Warnings, Is.Not.Empty);
        Assert.That(Pmc(sessionId).Inventory!.Items!.First(item => item.Id == stack).Upd!.StackObjectsCount, Is.EqualTo(10));
    }

    private PmcData Pmc(MongoId sessionId)
    {
        return _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
    }

    private MongoId AddProfile(int documents)
    {
        var sessionId = new MongoId();
        var stash = new MongoId();
        var sortingTable = new MongoId();
        _stack = new MongoId();
        var pmc = new PmcData
        {
            Id = sessionId,
            Bonuses = [],
            InsuredItems = [],
            Info = new SPTarkov.Server.Core.Models.Eft.Common.Tables.Info
            {
                Side = "Usec",
                Level = 5,
                GameVersion = GameEditions.STANDARD,
            },
            Inventory = new BotBaseInventory
            {
                Stash = stash,
                SortingTable = sortingTable,
                Items =
                [
                    new Item { Id = stash, Template = StandardStash },
                    new Item { Id = sortingTable, Template = SortingTable },
                    new Item
                    {
                        Id = _stack,
                        Template = FinanceDocument,
                        ParentId = stash,
                        SlotId = "hideout",
                        Location = new ItemLocation
                        {
                            X = 0,
                            Y = 0,
                            R = ItemRotation.Horizontal,
                        },
                        Upd = new Upd { StackObjectsCount = documents },
                    },
                ],
            },
        };

        _saveServer.AddProfile(
            new SptProfile
            {
                ProfileInfo = new SPTarkov.Server.Core.Models.Eft.Profile.Info { ProfileId = sessionId },
                CharacterData = new Characters { PmcData = pmc },
            }
        );

        return sessionId;
    }
}
