using NUnit.Framework;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.Commerce;

namespace UnitTests.Tests.Services;

[TestFixture]
public class PaymentServiceTests
{
    private PaymentService _paymentService;
    private MongoId _sessionId;
    private MongoId _stashId;

    [OneTimeSetUp]
    public void Initialize()
    {
        _paymentService = DI.GetInstance().GetService<PaymentService>();
    }

    [SetUp]
    public void SetUp()
    {
        _sessionId = new MongoId();
        _stashId = new MongoId();
    }

    /// <summary>
    /// Build a profile holding the given rouble stack sizes in the stash root, returned in creation order
    /// </summary>
    private (PmcData Profile, List<Item> MoneyStacks) CreateProfileWithRoubleStacks(params double[] stackSizes)
    {
        var stash = new Item { Id = _stashId, Template = ItemTpl.STASH_STANDARD_STASH_10X30 };
        var items = new List<Item> { stash };

        var moneyStacks = new List<Item>();
        foreach (var stackSize in stackSizes)
        {
            var stack = new Item
            {
                Id = new MongoId(),
                Template = Money.ROUBLES,
                ParentId = _stashId,
                SlotId = "hideout",
                Upd = new Upd { StackObjectsCount = stackSize },
            };

            moneyStacks.Add(stack);
            items.Add(stack);
        }

        var profile = new PmcData
        {
            Id = _sessionId,
            Inventory = new BotBaseInventory
            {
                Stash = _stashId,
                Items = items,
            },
            InsuredItems = [],
        };

        return (profile, moneyStacks);
    }

    private ItemEventRouterResponse CreateOutput()
    {
        return new ItemEventRouterResponse
        {
            Warnings = [],
            ProfileChanges = new Dictionary<MongoId, ProfileChange>
            {
                {
                    _sessionId,
                    new ProfileChange
                    {
                        Items = new ItemChanges
                        {
                            NewItems = [],
                            ChangedItems = [],
                            DeletedItems = [],
                        },
                    }
                },
            },
        };
    }

    private ProcessBuyTradeRequestData CreateFleaBuyRequest(MongoId sellerId, MongoId moneyStackId, double count)
    {
        return new ProcessBuyTradeRequestData
        {
            Action = "TradingConfirm",
            Type = "buy_from_ragfair_pmc",
            TransactionId = sellerId,
            ItemId = new MongoId(),
            Count = 1,
            SchemeId = 0,
            SchemeItems = [new IdWithCount { Id = moneyStackId, Count = count }],
        };
    }

    private static double TotalRoubles(PmcData profile)
    {
        return profile
            .Inventory!.Items!.Where(item => item.Template == Money.ROUBLES)
            .Sum(item => item.Upd?.StackObjectsCount ?? 0);
    }

    [Test]
    public void PayMoney_ManyOffersPaidWithOwnStack_SpendsTheStackEachOfferNominated()
    {
        // 9 single rouble stacks plus one large stack, exactly as reported
        var (profile, moneyStacks) = CreateProfileWithRoubleStacks(1, 1, 1, 1, 1, 1, 1, 1, 1, 100_000);
        var singleRoubleStacks = moneyStacks.Take(9).ToList();
        var largeStack = moneyStacks[9];
        var sellerId = new MongoId();

        var output = CreateOutput();

        // Client nominates the single rouble stacks in the reverse of profile order, so a server that picks its own
        // stacks diverges immediately
        foreach (var nominatedStack in Enumerable.Reverse(singleRoubleStacks))
        {
            _paymentService.PayMoney(profile, CreateFleaBuyRequest(sellerId, nominatedStack.Id, 1), _sessionId, output);
        }

        Assert.That(output.Warnings, Is.Empty, "Paying with the stacks the client nominated should never fail");
        Assert.That(profile.Inventory!.Items!.Where(item => item.Template == Money.ROUBLES).ToList(), Has.Count.EqualTo(1));
        Assert.That(largeStack.Upd!.StackObjectsCount, Is.EqualTo(100_000), "The large stack should not have been touched");
    }

    [Test]
    public void PayMoney_OfferNominatesStackSpentByAnEarlierOffer_ReportsTheMissingStack()
    {
        var (profile, moneyStacks) = CreateProfileWithRoubleStacks(1, 100_000);
        var spentStack = moneyStacks[0];
        var sellerId = new MongoId();

        var output = CreateOutput();
        _paymentService.PayMoney(profile, CreateFleaBuyRequest(sellerId, spentStack.Id, 1), _sessionId, output);
        Assert.That(output.Warnings, Is.Empty);

        // Same stack again, it no longer exists
        _paymentService.PayMoney(profile, CreateFleaBuyRequest(sellerId, spentStack.Id, 1), _sessionId, output);

        Assert.That(output.Warnings, Has.Count.EqualTo(1));
        Assert.That(
            output.Warnings![0].ErrorMessage,
            Does.Contain(spentStack.Id.ToString()),
            "A missing stack must be named, not reported as a currency the profile has none of"
        );
        Assert.That(TotalRoubles(profile), Is.EqualTo(100_000), "A failed payment must not take money");
    }

    /// <summary>
    /// `SptInsure`, repairs, healing and the flea listing fee all put a currency tpl in `id` rather than a stack id
    /// </summary>
    [Test]
    public void PayMoney_SchemeItemIsCurrencyTpl_TakesMoneyFromProfile()
    {
        var (profile, _) = CreateProfileWithRoubleStacks(50_000);
        var output = CreateOutput();

        var request = new ProcessBuyTradeRequestData
        {
            Action = "SptInsure",
            Type = string.Empty,
            TransactionId = new MongoId(),
            ItemId = MongoId.Empty(),
            Count = 0,
            SchemeId = 0,
            SchemeItems = [new IdWithCount { Id = Money.ROUBLES, Count = 12_000 }],
        };

        _paymentService.PayMoney(profile, request, _sessionId, output);

        Assert.That(output.Warnings, Is.Empty);
        Assert.That(TotalRoubles(profile), Is.EqualTo(38_000));
    }

    [Test]
    public void PayMoney_NotEnoughMoney_TakesNothing()
    {
        var (profile, moneyStacks) = CreateProfileWithRoubleStacks(500);
        var output = CreateOutput();

        _paymentService.PayMoney(profile, CreateFleaBuyRequest(new MongoId(), moneyStacks[0].Id, 900), _sessionId, output);

        Assert.That(output.Warnings, Has.Count.EqualTo(1));
        Assert.That(TotalRoubles(profile), Is.EqualTo(500));
    }

    [Test]
    public void AddPaymentToOutput_NoRequestedStacks_SpendsSmallestStackFirst()
    {
        var (profile, moneyStacks) = CreateProfileWithRoubleStacks(100_000, 5_000, 300);
        var output = CreateOutput();

        _paymentService.AddPaymentToOutput(profile, Money.ROUBLES, 300, _sessionId, output, null);

        Assert.That(output.Warnings, Is.Empty);
        Assert.That(profile.Inventory!.Items!.Any(item => item.Id == moneyStacks[2].Id), Is.False);
        Assert.That(moneyStacks[0].Upd!.StackObjectsCount, Is.EqualTo(100_000));
        Assert.That(moneyStacks[1].Upd!.StackObjectsCount, Is.EqualTo(5_000));
    }

    [Test]
    public void AddPaymentToOutput_RequestedStackTooSmall_TakesRemainderFromOtherStacks()
    {
        var (profile, moneyStacks) = CreateProfileWithRoubleStacks(100_000, 400);
        var output = CreateOutput();

        _paymentService.AddPaymentToOutput(profile, Money.ROUBLES, 1_000, _sessionId, output, new HashSet<MongoId> { moneyStacks[1].Id });

        Assert.That(output.Warnings, Is.Empty);
        Assert.That(profile.Inventory!.Items!.Any(item => item.Id == moneyStacks[1].Id), Is.False, "Nominated stack is spent first");
        Assert.That(moneyStacks[0].Upd!.StackObjectsCount, Is.EqualTo(99_400), "Remainder comes from the other stacks");
    }
}
